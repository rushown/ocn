using EWallet.Domain.Enums;

namespace EWallet.Application.DTOs;

public record WalletDto(
    Guid Id,
    Guid UserId,
    decimal Balance,
    string Currency,
    bool IsLocked,
    DateTime CreatedAt);

public record TransactionDto(
    Guid Id,
    Guid WalletId,
    decimal Amount,
    string Currency,
    TransactionType Type,
    TransactionStatus Status,
    string? Description,
    string IdempotencyKey,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public record TransferRequest(
    Guid ReceiverWalletId,
    decimal Amount,
    string Currency,
    string? Description,
    string? OtpCode,
    string IdempotencyKey);

public record DepositRequest(
    decimal Amount,
    string Currency,
    string ExternalReference);

public record WithdrawRequest(
    decimal Amount,
    string Currency,
    string ExternalReference);

public record BalanceDto(
    Guid WalletId,
    decimal Balance,
    string Currency,
    decimal AvailableLimit);

public record PagedTransactionRequest(
    int Page = 1,
    int PageSize = 20);
