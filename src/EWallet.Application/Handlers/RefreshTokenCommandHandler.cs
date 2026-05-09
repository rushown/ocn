using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwtService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUnitOfWork uow,
        IJwtService jwtService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _uow = uow;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        try
        {
            var user = await _uow.Users.GetByRefreshTokenAsync(request.RefreshToken, ct);
            if (user is null)
                return Result<AuthResponse>.Failure("Invalid refresh token.");

            if (user.RefreshTokenExpiry < DateTime.UtcNow)
                return Result<AuthResponse>.Failure("Refresh token has expired.");

            var newRefreshToken = _jwtService.GenerateRefreshToken();
            user.UpdateRefreshToken(newRefreshToken, DateTime.UtcNow.AddDays(30));
            await _uow.SaveChangesAsync(ct);

            var accessToken = _jwtService.GenerateAccessToken(user);

            var response = new AuthResponse(
                AccessToken: accessToken,
                RefreshToken: newRefreshToken,
                ExpiresAt: DateTime.UtcNow.AddHours(1),
                UserId: user.Id,
                Email: user.Email);

            _logger.LogInformation("Token refreshed for user {UserId}", user.Id);
            return Result<AuthResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return Result<AuthResponse>.Failure("Token refresh failed.");
        }
    }
}
