using System.Diagnostics;
using System.Security.Claims;

namespace EWallet.API.Middleware;

/// <summary>
/// Logs each HTTP request with method, path, status, elapsed ms, UserId, and correlation ID.
/// Generates and propagates an X-Correlation-ID header on every request.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Generate or propagate correlation ID
        var correlationId = ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        ctx.Items["X-Correlation-ID"] = correlationId;
        ctx.Response.Headers["X-Correlation-ID"] = correlationId;

        // Add security headers
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(ctx);
        }
        finally
        {
            sw.Stop();

            var userId = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var statusCode = ctx.Response.StatusCode;
            var level = statusCode >= 500 ? LogLevel.Error
                      : statusCode >= 400 ? LogLevel.Warning
                      : LogLevel.Information;

            _logger.Log(level,
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms | User: {UserId} | CorrelationId: {CorrelationId}",
                ctx.Request.Method,
                ctx.Request.Path,
                statusCode,
                sw.ElapsedMilliseconds,
                userId,
                correlationId);
        }
    }
}

/// <summary>Extension method for cleaner middleware registration</summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
