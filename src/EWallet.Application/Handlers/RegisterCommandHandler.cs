using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Interfaces;
using EWallet.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        ILogger<RegisterCommandHandler> logger)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Registering new user with email {Email}", request.Email);

        try
        {
            var existingUser = await _uow.Users.GetByEmailAsync(request.Email, ct);
            if (existingUser is not null)
                return Result<AuthResponse>.Failure("Email is already in use.", ErrorCodes.UserNotFound);

            var passwordHash = _passwordHasher.Hash(request.Password);
            var user = User.Create(request.Email, request.PhoneNumber, request.FullName, passwordHash);

            var refreshToken = _jwtService.GenerateRefreshToken();
            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(30));

            var wallet = Wallet.Create(user.Id, "USD");

            await _uow.BeginTransactionAsync(ct);
            await _uow.Users.AddAsync(user, ct);
            await _uow.Wallets.AddAsync(wallet, ct);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);

            var accessToken = _jwtService.GenerateAccessToken(user);

            var response = new AuthResponse(
                AccessToken: accessToken,
                RefreshToken: refreshToken,
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                UserId: user.Id,
                Email: user.Email);

            _logger.LogInformation("User {UserId} registered successfully", user.Id);
            return Result<AuthResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user with email {Email}", request.Email);
            await _uow.RollbackTransactionAsync(ct);
            return Result<AuthResponse>.Failure("Registration failed. Please try again.");
        }
    }
}
