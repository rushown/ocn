using EWallet.Application.Auth.Commands;
using EWallet.Application.Auth.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EWallet.API.Controllers;

/// <summary>Authentication endpoints</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Register a new user account</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var command = new RegisterCommand(
            request.Email,
            request.PhoneNumber,
            request.FullName,
            request.Password);

        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Register), result.Value)
            : BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
    }

    /// <summary>Login and receive JWT tokens</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Login Failed",
                Detail = result.Error,
                Status = StatusCodes.Status401Unauthorized
            });
        }

        _logger.LogInformation("User {Email} logged in successfully", request.Email);
        return Ok(result.Value);
    }

    /// <summary>Refresh access token using refresh token</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken ct)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new ProblemDetails
            {
                Title = "Token Refresh Failed",
                Detail = result.Error,
                Status = StatusCodes.Status401Unauthorized
            });
    }

    /// <summary>Logout and revoke refresh token</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var command = new LogoutCommand(userId, request.RefreshToken);
        await _mediator.Send(command, ct);

        _logger.LogInformation("User {UserId} logged out", userId);
        return NoContent();
    }
}
