using AutoMapper;
using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Interfaces;
using EWallet.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, Result<WalletDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateWalletCommandHandler> _logger;

    public CreateWalletCommandHandler(
        IUnitOfWork uow,
        IMapper mapper,
        ILogger<CreateWalletCommandHandler> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<WalletDto>> Handle(CreateWalletCommand request, CancellationToken ct)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
            if (user is null)
                return Result<WalletDto>.Failure("User not found.", ErrorCodes.UserNotFound);

            var existing = await _uow.Wallets.GetByUserIdAsync(request.UserId, ct);
            if (existing is not null)
                return Result<WalletDto>.Failure("User already has a wallet.");

            var wallet = Wallet.Create(request.UserId, request.Currency);
            await _uow.Wallets.AddAsync(wallet, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Wallet {WalletId} created for user {UserId}", wallet.Id, request.UserId);
            return Result<WalletDto>.Success(_mapper.Map<WalletDto>(wallet));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet for user {UserId}", request.UserId);
            return Result<WalletDto>.Failure("Wallet creation failed.");
        }
    }
}
