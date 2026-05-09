using AutoMapper;
using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Interfaces;
using EWallet.Domain.Entities;
using EWallet.Domain.Enums;
using EWallet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class WithdrawCommandHandler : IRequestHandler<WithdrawCommand, Result<TransactionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IWalletNotificationService _notifications;
    private readonly IMapper _mapper;
    private readonly ILogger<WithdrawCommandHandler> _logger;

    public WithdrawCommandHandler(
        IUnitOfWork uow,
        IPaymentGateway paymentGateway,
        IWalletNotificationService notifications,
        IMapper mapper,
        ILogger<WithdrawCommandHandler> logger)
    {
        _uow = uow;
        _paymentGateway = paymentGateway;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> Handle(WithdrawCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Withdrawal requested for user {UserId}, amount={Amount} {Currency}",
            request.UserId, request.Amount, request.Currency);

        try
        {
            var wallet = await _uow.Wallets.GetByUserIdAsync(request.UserId, ct);
            if (wallet is null)
                return Result<TransactionDto>.Failure("Wallet not found.", ErrorCodes.WalletNotFound);

            if (wallet.IsLocked)
                return Result<TransactionDto>.Failure("Wallet is locked.", ErrorCodes.WalletLocked);

            var money = new Money(request.Amount, request.Currency);

            if (wallet.Balance.Amount < money.Amount)
                return Result<TransactionDto>.Failure("Insufficient funds.", ErrorCodes.InsufficientFunds);

            var gatewayResult = await _paymentGateway.ProcessWithdrawalAsync(wallet.Id, money, request.ExternalRef, ct);

            Transaction transaction;

            await _uow.BeginTransactionAsync(ct);

            if (gatewayResult.IsSuccess)
            {
                wallet.Debit(money, "Withdrawal");
                transaction = Transaction.Create(wallet.Id, money, TransactionType.Withdrawal, request.IdempotencyKey);
                transaction.Complete();

                var audit = AuditLog.Create(
                    entityId: wallet.Id,
                    entityType: "Wallet",
                    action: "WITHDRAWAL",
                    oldValues: null,
                    newValues: $"{{\"amount\":\"{money}\",\"gatewayRef\":\"{gatewayResult.TransactionRef}\"}}",
                    userId: request.UserId,
                    ip: "system");
                await _uow.AuditLogs.AddAsync(audit, ct);
            }
            else
            {
                transaction = Transaction.Create(wallet.Id, money, TransactionType.Withdrawal, request.IdempotencyKey);
                transaction.Fail(gatewayResult.ErrorMessage ?? "Gateway error");
            }

            await _uow.Transactions.AddAsync(transaction, ct);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);

            if (gatewayResult.IsSuccess)
            {
                await _notifications.NotifyBalanceUpdatedAsync(request.UserId, wallet.Balance, ct);
                await _notifications.NotifyTransactionStatusChangedAsync(request.UserId, transaction.Id, transaction.Status, ct);
            }

            var dto = _mapper.Map<TransactionDto>(transaction);

            if (!gatewayResult.IsSuccess)
            {
                _logger.LogWarning("Withdrawal failed for user {UserId}: {Error}", request.UserId, gatewayResult.ErrorMessage);
                return Result<TransactionDto>.Failure(gatewayResult.ErrorMessage ?? "Withdrawal failed.");
            }

            _logger.LogInformation("Withdrawal {TransactionId} completed for user {UserId}", transaction.Id, request.UserId);
            return Result<TransactionDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Withdrawal processing error for user {UserId}", request.UserId);
            await _uow.RollbackTransactionAsync(ct);
            return Result<TransactionDto>.Failure("Withdrawal failed. Please try again.");
        }
    }
}
