using System.Diagnostics;
using EWallet.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUser.UserId?.ToString() ?? "anonymous";

        _logger.LogInformation(
            "Handling {RequestName} | UserId={UserId}",
            requestName, userId);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "Slow request {RequestName} | UserId={UserId} | Elapsed={ElapsedMs}ms",
                    requestName, userId, sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "Handled {RequestName} | UserId={UserId} | Elapsed={ElapsedMs}ms",
                    requestName, userId, sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Error handling {RequestName} | UserId={UserId} | Elapsed={ElapsedMs}ms",
                requestName, userId, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
