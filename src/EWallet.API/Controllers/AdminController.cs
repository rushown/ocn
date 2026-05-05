using EWallet.Application.Admin.Commands;
using EWallet.Application.Admin.DTOs;
using EWallet.Application.Admin.Queries;
using EWallet.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EWallet.API.Controllers;

/// <summary>Admin-only wallet and audit management endpoints</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IMediator mediator, ILogger<AdminController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Lock a wallet — prevents all outgoing transactions</summary>
    [HttpPost("wallet/{walletId:guid}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LockWallet(
        Guid walletId,
        [FromBody] LockWalletRequest request,
        CancellationToken ct)
    {
        var command = new LockWalletCommand(walletId, request.Reason);
        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode == "WALLET_NOT_FOUND"
                ? NotFound()
                : BadRequest(new ProblemDetails
                {
                    Title = result.Error,
                    Detail = result.ErrorCode,
                    Status = StatusCodes.Status400BadRequest
                });
        }

        _logger.LogWarning("Admin locked wallet {WalletId}. Reason: {Reason}", walletId, request.Reason);
        return NoContent();
    }

    /// <summary>Unlock a previously locked wallet</summary>
    [HttpPost("wallet/{walletId:guid}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnlockWallet(
        Guid walletId,
        CancellationToken ct)
    {
        var command = new UnlockWalletCommand(walletId);
        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return result.ErrorCode == "WALLET_NOT_FOUND"
                ? NotFound()
                : BadRequest(new ProblemDetails
                {
                    Title = result.Error,
                    Detail = result.ErrorCode,
                    Status = StatusCodes.Status400BadRequest
                });
        }

        _logger.LogInformation("Admin unlocked wallet {WalletId}", walletId);
        return NoContent();
    }

    /// <summary>Get paginated audit logs — filterable by user, action type, and date range</summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetAuditLogs(
        [FromQuery] Guid? userId,
        [FromQuery] string? actionType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new GetAuditLogsQuery(userId, actionType, from, to, page, pageSize);
        var result = await _mediator.Send(query, ct);

        return Ok(result);
    }
}
