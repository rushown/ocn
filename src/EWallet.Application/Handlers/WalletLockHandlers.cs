using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.Interfaces;
using EWallet.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class LockWalletCommandHandler : IRequestHandler<LockWalletCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<LockWalletCommandHandler> _logger;

    public LockWalletCommandHandler(IUnitOfWork uow, ILogger<LockWalletCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result> Handle(LockWalletCommand request, CancellationToken ct)
    {
        try
        {
            var wallet = await _uow.Wallets.GetByIdAsync(request.WalletId, ct);
            if (wallet is null)
                return Result.Failure("Wallet not found.", ErrorCodes.WalletNotFound);

            wallet.Lock(request.Reason);

            var audit = AuditLog.Create(
                entityId: wallet.Id,
                entityType: "Wallet",
                action: "WALLET_LOCKED",
                oldValues: null,
                newValues: $"{{\"reason\":\"{request.Reason}\"}}",
                userId: null,
                ip: "system");
            await _uow.AuditLogs.AddAsync(audit, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Wallet {WalletId} locked. Reason: {Reason}", request.WalletId, request.Reason);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking wallet {WalletId}", request.WalletId);
            return Result.Failure("Failed to lock wallet.");
        }
    }
}

public class UnlockWalletCommandHandler : IRequestHandler<UnlockWalletCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UnlockWalletCommandHandler> _logger;

    public UnlockWalletCommandHandler(IUnitOfWork uow, ILogger<UnlockWalletCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result> Handle(UnlockWalletCommand request, CancellationToken ct)
    {
        try
        {
            var wallet = await _uow.Wallets.GetByIdAsync(request.WalletId, ct);
            if (wallet is null)
                return Result.Failure("Wallet not found.", ErrorCodes.WalletNotFound);

            wallet.Unlock();

            var audit = AuditLog.Create(
                entityId: wallet.Id,
                entityType: "Wallet",
                action: "WALLET_UNLOCKED",
                oldValues: null,
                newValues: null,
                userId: null,
                ip: "system");
            await _uow.AuditLogs.AddAsync(audit, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Wallet {WalletId} unlocked", request.WalletId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking wallet {WalletId}", request.WalletId);
            return Result.Failure("Failed to unlock wallet.");
        }
    }
}
