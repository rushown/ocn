using EWallet.API.Models;
using EWallet.Application.Commands;
using EWallet.Application.Common;
using EWallet.Application.DTOs;
using EWallet.Application.Queries;
using EWallet.API.Filters;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace EWallet.API.Controllers;

/// <summary>Wallet operations endpoints</summary>
[ApiController]
[Route("api/wallet")]
[Authorize]
[EnableRateLimiting("WalletOps")]
[Produces("application/json")]
public class WalletController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WalletController> _logger;

    public WalletController(IMediator mediator, ILogger<WalletController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Get current user's wallet balance and daily limit remaining</summary>
    [HttpGet("balance")]
    [ProducesResponseType(typeof(BalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceDto>> GetBalance(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetWalletBalanceQuery(userId), ct);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    /// <summary>Lookup recipient wallet metadata by wallet id</summary>
    [HttpGet("lookup/{walletId:guid}")]
    [ProducesResponseType(typeof(WalletLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WalletLookupDto>> LookupWallet(Guid walletId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWalletLookupQuery(walletId), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    /// <summary>Deposit funds to wallet</summary>
    [HttpPost("deposit")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransactionDto>> Deposit(
        [FromBody] EWallet.API.Models.DepositRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var command = new DepositCommand(userId, request.Amount, request.Currency, request.ExternalRef, idempotencyKey);
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTransaction), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
    }

    /// <summary>Withdraw funds from wallet</summary>
    [HttpPost("withdraw")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransactionDto>> Withdraw(
        [FromBody] EWallet.API.Models.WithdrawRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var command = new WithdrawCommand(userId, request.Amount, request.Currency, request.ExternalRef, idempotencyKey);
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTransaction), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
    }

    /// <summary>Transfer funds to another wallet. Requires OTP verification if amount > $500.</summary>
    [HttpPost("transfer")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TransactionDto>> Transfer(
        [FromBody] EWallet.API.Models.TransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var command = new TransferCommand(
            userId,
            request.RecipientWalletId,
            request.Amount,
            request.Currency,
            request.Description,
            request.OtpCode,
            idempotencyKey);

        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTransaction), new { id = result.Value!.Id }, result.Value)
            : BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
    }

    /// <summary>Get paginated transaction history for the authenticated user</summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PagedResult<TransactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TransactionDto>>> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var query = new GetTransactionHistoryQuery(userId, page, pageSize);
        var result = await _mediator.Send(query, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
    }

    /// <summary>Get a single transaction by ID</summary>
    [HttpGet("transactions/{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransactionDto>> GetTransaction(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var query = new GetTransactionByIdQuery(id, userId);
        var result = await _mediator.Send(query, ct);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    private Guid GetCurrentUserId()
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
