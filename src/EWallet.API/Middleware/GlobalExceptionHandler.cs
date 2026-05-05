using EWallet.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EWallet.API.Middleware;

/// <summary>
/// Centralised exception handler using ASP.NET 8 IExceptionHandler.
/// Maps domain exceptions → appropriate HTTP status codes and ProblemDetails responses.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, title, detail) = exception switch
        {
            ValidationException ve =>
                (StatusCodes.Status400BadRequest,
                 "Validation Failed",
                 string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),

            InsufficientFundsException =>
                (StatusCodes.Status400BadRequest,
                 "Insufficient Funds",
                 exception.Message),

            DailyLimitExceededException =>
                (StatusCodes.Status400BadRequest,
                 "Daily Limit Exceeded",
                 exception.Message),

            WalletLockedException =>
                (StatusCodes.Status403Forbidden,
                 "Wallet Locked",
                 exception.Message),

            OtpRequiredException =>
                (StatusCodes.Status403Forbidden,
                 "OTP Required",
                 exception.Message),

            OtpVerificationFailedException =>
                (StatusCodes.Status400BadRequest,
                 "OTP Verification Failed",
                 exception.Message),

            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict,
                 "Concurrency Conflict",
                 "The resource was modified by another process. Please retry your request."),

            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized,
                 "Unauthorized",
                 exception.Message),

            KeyNotFoundException =>
                (StatusCodes.Status404NotFound,
                 "Not Found",
                 exception.Message),

            _ =>
                (StatusCodes.Status500InternalServerError,
                 "Internal Server Error",
                 "An unexpected error occurred. Please try again later.")
        };

        // Log 5xx as errors, 4xx as warnings
        if (statusCode >= 500)
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
        else
            _logger.LogWarning(exception, "Handled exception on {Method} {Path}: {Title}", ctx.Request.Method, ctx.Request.Path, title);

        ctx.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = ctx.Request.Path
        };

        // Attach correlation ID if available
        if (ctx.Items.TryGetValue("X-Correlation-ID", out var correlationId))
            problem.Extensions["correlationId"] = correlationId;

        await ctx.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
