using EWallet.Application.Common;
using EWallet.Application.DTOs;
using MediatR;

namespace EWallet.Application.Commands;

public record CreateWalletCommand(
    Guid UserId,
    string Currency) : IRequest<Result<WalletDto>>;

public record DepositCommand(
    Guid UserId,
    decimal Amount,
    string Currency,
    string ExternalRef,
    string IdempotencyKey) : IRequest<Result<TransactionDto>>, IIdempotentCommand;

public record WithdrawCommand(
    Guid UserId,
    decimal Amount,
    string Currency,
    string ExternalRef,
    string IdempotencyKey) : IRequest<Result<TransactionDto>>, IIdempotentCommand;

public record TransferCommand(
    Guid SenderUserId,
    Guid ReceiverWalletId,
    decimal Amount,
    string Currency,
    string? Description,
    string? OtpCode,
    string IdempotencyKey) : IRequest<Result<TransactionDto>>, IIdempotentCommand;

public record LockWalletCommand(
    Guid WalletId,
    string Reason) : IRequest<Result>;

public record UnlockWalletCommand(
    Guid WalletId) : IRequest<Result>;
