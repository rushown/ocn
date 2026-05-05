using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Handlers;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(IUnitOfWork uow, ILogger<LogoutCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        try
        {
            var user = await _uow.Users.GetByIdAsync(request.UserId, ct);
            if (user is null)
                return Result.Failure("User not found.", ErrorCodes.UserNotFound);

            user.ClearRefreshToken();
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("User {UserId} logged out", request.UserId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {UserId}", request.UserId);
            return Result.Failure("Logout failed.");
        }
    }
}
