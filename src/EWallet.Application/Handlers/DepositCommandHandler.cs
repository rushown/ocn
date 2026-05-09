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

public class DepositCommandHandler : IRequestHandler<DepositCommand, Result<TransactionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IWalletNotificationService _notifications;
    private readonly IMapper _mapper;
    private readonly ILogger<DepositCommandHandler> _logger;

    public DepositCommandHandler(
        IUnitOfWork uow,
        IPaymentGateway paymentGateway,
        IWalletNotificationService notifications,
        IMapper mapper,
        ILogger<DepositCommandHandler> logger)
    {
        _uow = uow;
        _paymentGateway = paymentGateway;
        _notifications = notifications;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> Handle(DepositCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Deposit requested for user {UserId}, amount={Amount} {Currency}",
            request.UserId, request.Amount, request.Currency);

        try
        {
            var wallet = await _uow.Wallets.GetByUserIdAsync(request.UserId, ct);
            if (wallet is null)
                return Result<TransactionDto>.Failure("Wallet not found.", ErrorCodes.WalletNotFound);

            if (wallet.IsLocked)
                return Result<TransactionDto>.Failure("Wallet is locked.", ErrorCodes.WalletLocked);

            var money = new Money(request.Amount, request.Currency);

            // Call payment gateway
            var gatewayResult = await _paymentGateway.ProcessDepositAsync(wallet.Id, money, request.ExternalRef, ct);

            Transaction transaction;

            await _uow.BeginTransactionAsync(ct);

            if (gatewayResult.IsSuccess)
            {
                // Credit wallet, create completed transaction
                wallet.Credit(money, "Deposit");
                transaction = Transaction.Create(wallet.Id, money, TransactionType.Deposit, request.IdempotencyKey);
                transaction.Complete();

                var audit = AuditLog.Create(
                    entityId: wallet.Id,
                    entityType: "Wallet",
                    action: "DEPOSIT",
                    oldValues: null,
                    newValues: $"{{\"amount\":\"{money}\",\"gatewayRef\":\"{gatewayResult.TransactionRef}\"}}",
                    userId: request.UserId,
                    ip: "system");
                await _uow.AuditLogs.AddAsync(audit, ct);
            }
            else
            {
                // Create failed transaction
                transaction = Transaction.Create(wallet.Id, money, TransactionType.Deposit, request.IdempotencyKey);
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
                _logger.LogWarning("Deposit failed for user {UserId}: {Error}", request.UserId, gatewayResult.ErrorMessage);
                return Result<TransactionDto>.Failure(gatewayResult.ErrorMessage ?? "Deposit failed.");
            }

            _logger.LogInformation("Deposit {TransactionId} completed for user {UserId}", transaction.Id, request.UserId);
            return Result<TransactionDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deposit processing error for user {UserId}", request.UserId);
            await _uow.RollbackTransactionAsync(ct);
            return Result<TransactionDto>.Failure("Deposit failed. Please try again.");
        }
    }
}
