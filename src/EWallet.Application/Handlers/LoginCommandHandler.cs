using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        ILogger<LoginCommandHandler> logger)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Login attempt for email {Email}", request.Email);

        try
        {
            var user = await _uow.Users.FindByEmailAsync(request.Email, ct);
            if (user is null)
                return Result<AuthResponse>.Failure("Invalid credentials.", ErrorCodes.UserNotFound);

            if (!user.IsActive)
                return Result<AuthResponse>.Failure("Account is disabled.");

            if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
                return Result<AuthResponse>.Failure("Invalid credentials.");

            var refreshToken = _jwtService.GenerateRefreshToken();
            user.UpdateRefreshToken(refreshToken, DateTime.UtcNow.AddDays(30));

            await _uow.SaveChangesAsync(ct);

            var accessToken = _jwtService.GenerateAccessToken(user);

            var response = new AuthResponse(
                AccessToken: accessToken,
                RefreshToken: refreshToken,
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                UserId: user.Id,
                Email: user.Email);

            _logger.LogInformation("User {UserId} logged in successfully", user.Id);
            return Result<AuthResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for email {Email}", request.Email);
            return Result<AuthResponse>.Failure("Login failed. Please try again.");
        }
    }
}
