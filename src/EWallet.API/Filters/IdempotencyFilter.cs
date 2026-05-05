using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.RegularExpressions;

namespace EWallet.API.Filters;

/// <summary>
/// Action filter that enforces the presence and format of the Idempotency-Key header.
/// Applied to all wallet mutation endpoints (deposit, withdraw, transfer).
///
/// Rules:
///   - Header must be present
///   - Value must match ^[a-zA-Z0-9_\-]{8,128}$
///   - Validated key is stored in HttpContext.Items["IdempotencyKey"] for handler access
/// </summary>
public class IdempotencyFilter : IActionFilter
{
    private static readonly Regex KeyPattern =
        new(@"^[a-zA-Z0-9_\-]{8,128}$", RegexOptions.Compiled);

    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(ILogger<IdempotencyFilter> logger)
    {
        _logger = logger;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning(
                "Request to {Path} missing Idempotency-Key header",
                context.HttpContext.Request.Path);

            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Title = "Missing Idempotency-Key",
                Detail = "The Idempotency-Key header is required for this operation.",
                Status = StatusCodes.Status400BadRequest
            });
            return;
        }

        if (!KeyPattern.IsMatch(key))
        {
            _logger.LogWarning(
                "Invalid Idempotency-Key format for {Path}: {Key}",
                context.HttpContext.Request.Path, key);

            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Title = "Invalid Idempotency-Key",
                Detail = "Idempotency-Key must be 8–128 characters and contain only letters, digits, underscores, or hyphens.",
                Status = StatusCodes.Status400BadRequest
            });
            return;
        }

        // Store validated key for use by the command handler
        context.HttpContext.Items["IdempotencyKey"] = key;
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
