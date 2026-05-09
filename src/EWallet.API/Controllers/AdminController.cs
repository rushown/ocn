using EWallet.API.Models;
using EWallet.Application.Commands;
using EWallet.Application.Common;
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
}
