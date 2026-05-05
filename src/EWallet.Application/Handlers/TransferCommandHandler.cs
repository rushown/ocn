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

public class TransferCommandHandler : IRequestHandler<TransferCommand, Result<TransactionDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IIdempotencyService _idempotency;
    private readonly IWalletNotificationService _notifications;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILogger<TransferCommandHandler> _logger;

    // KYC-based daily transfer limits
    private static readonly Dictionary<KycLevel, decimal> DailyLimits = new()
    {
        [KycLevel.None]     = 500m,
        [KycLevel.Basic]    = 5_000m,
        [KycLevel.Enhanced] = 50_000m,
        [KycLevel.Full]     = 100_000m,
    };

    private const decimal OtpThreshold           = 500m;
    private const decimal PessimisticLockThreshold = 1_000m;

    public TransferCommandHandler(
        IUnitOfWork uow,
        IIdempotencyService idempotency,
        IWalletNotificationService notifications,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILogger<TransferCommandHandler> logger)
    {
        _uow = uow;
        _idempotency = idempotency;
        _notifications = notifications;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<TransactionDto>> Handle(TransferCommand request, CancellationToken ct)
    {
        _logger.LogInformation(
            "Transfer requested: sender={SenderUserId} receiver={ReceiverWalletId} amount={Amount} {Currency} key={Key}",
            request.SenderUserId, request.ReceiverWalletId, request.Amount, request.Currency, request.IdempotencyKey);

        try
        {
            // 1. Check idempotency — return cached result if this key was already processed
            var cached = await _idempotency.GetCachedResponseAsync<TransactionDto>(request.IdempotencyKey, ct);
            if (cached is not null)
            {
                _logger.LogInformation("Idempotency hit for key {Key}", request.IdempotencyKey);
                return Result<TransactionDto>.Failure("Duplicate transaction.", ErrorCodes.DuplicateTransaction);
            }

            // 2. Validate sender owns a wallet
            var senderWallet = await _uow.Wallets.FindByUserIdAsync(request.SenderUserId, ct);
            if (senderWallet is null)
                return Result<TransactionDto>.Failure("Sender wallet not found.", ErrorCodes.WalletNotFound);

            if (senderWallet.IsLocked)
                return Result<TransactionDto>.Failure("Sender wallet is locked.", ErrorCodes.WalletLocked);

            // 3. Check daily limit based on KYC level
            var user = await _uow.Users.GetByIdAsync(request.SenderUserId, ct);
            if (user is null)
                return Result<TransactionDto>.Failure("User not found.", ErrorCodes.UserNotFound);

            var dailyLimit = DailyLimits.GetValueOrDefault(user.KycLevel, 500m);
            var dailySpent = await _uow.Transactions.GetDailyTransferTotalAsync(senderWallet.Id, DateTime.UtcNow.Date, ct);
            if (dailySpent + request.Amount > dailyLimit)
                return Result<TransactionDto>.Failure("Daily transfer limit exceeded.", ErrorCodes.DailyLimitExceeded);

            // 4. OTP required for transfers > $500
            if (request.Amount > OtpThreshold)
            {
                if (string.IsNullOrWhiteSpace(request.OtpCode))
                    return Result<TransactionDto>.Failure("OTP is required for transfers above $500.", ErrorCodes.InvalidOtp);

                var otpValid = await _uow.Users.ValidateOtpAsync(request.SenderUserId, request.OtpCode, "Transfer", ct);
                if (!otpValid)
                    return Result<TransactionDto>.Failure("Invalid or expired OTP.", ErrorCodes.InvalidOtp);
            }

            // 5. For large transfers use pessimistic locking to prevent race conditions
            EWallet.Domain.Entities.Wallet? senderLocked = null;
            if (request.Amount > PessimisticLockThreshold)
            {
                senderLocked = await _uow.Wallets.GetByIdWithLockAsync(senderWallet.Id, ct);
                if (senderLocked is null)
                    return Result<TransactionDto>.Failure("Could not acquire lock on sender wallet.", ErrorCodes.ConcurrencyConflict);
                senderWallet = senderLocked;
            }

            var receiverWallet = await _uow.Wallets.GetByIdAsync(request.ReceiverWalletId, ct);
            if (receiverWallet is null)
                return Result<TransactionDto>.Failure("Receiver wallet not found.", ErrorCodes.WalletNotFound);

            if (receiverWallet.IsLocked)
                return Result<TransactionDto>.Failure("Receiver wallet is locked.", ErrorCodes.WalletLocked);

            var money = new Money(request.Amount, request.Currency);

            // Check sender has sufficient balance
            if (senderWallet.Balance.Amount < money.Amount)
                return Result<TransactionDto>.Failure("Insufficient funds.", ErrorCodes.InsufficientFunds);

            // 6. Begin UoW transaction
            await _uow.BeginTransactionAsync(ct);

            // 7. Debit sender
            senderWallet.Debit(money);

            // 8. Credit receiver
            receiverWallet.Credit(money);

            // 9. Create Transaction entity (Pending → Completed)
            var transaction = Transaction.Create(
                walletId: senderWallet.Id,
                type: TransactionType.Transfer,
                amount: money,
                idempotencyKey: request.IdempotencyKey,
                description: request.Description);

            transaction.MarkCompleted();

            // 10. Write AuditLog entries for both wallets
            var auditSender = AuditLog.Create(senderWallet.Id, "TRANSFER_DEBIT",
                $"Transferred {money} to wallet {receiverWallet.Id}. TxId={transaction.Id}");
            var auditReceiver = AuditLog.Create(receiverWallet.Id, "TRANSFER_CREDIT",
                $"Received {money} from wallet {senderWallet.Id}. TxId={transaction.Id}");

            await _uow.Transactions.AddAsync(transaction, ct);
            await _uow.AuditLogs.AddAsync(auditSender, ct);
            await _uow.AuditLogs.AddAsync(auditReceiver, ct);
            await _uow.SaveChangesAsync(ct);

            // 11. Commit
            await _uow.CommitTransactionAsync(ct);

            var dto = _mapper.Map<TransactionDto>(transaction);

            // 12. Publish SignalR notifications to both users
            await _notifications.NotifyBalanceUpdatedAsync(request.SenderUserId, senderWallet.Balance, ct);
            await _notifications.NotifyTransactionStatusChangedAsync(request.SenderUserId, transaction.Id, transaction.Status, ct);

            if (receiverWallet.UserId != request.SenderUserId)
            {
                await _notifications.NotifyBalanceUpdatedAsync(receiverWallet.UserId, receiverWallet.Balance, ct);
                await _notifications.NotifyTransactionStatusChangedAsync(receiverWallet.UserId, transaction.Id, transaction.Status, ct);
            }

            // 13. Cache idempotency result (24h TTL)
            await _idempotency.StoreResponseAsync(request.IdempotencyKey, dto, TimeSpan.FromHours(24), ct);

            _logger.LogInformation("Transfer {TransactionId} completed successfully", transaction.Id);

            // 14. Return success
            return Result<TransactionDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer failed for key {Key}", request.IdempotencyKey);
            await _uow.RollbackTransactionAsync(ct);
            return Result<TransactionDto>.Failure("Transfer failed. Please try again.");
        }
    }
}
