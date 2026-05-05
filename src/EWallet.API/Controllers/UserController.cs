using EWallet.Application.User.Commands;
using EWallet.Application.User.DTOs;
using EWallet.Application.User.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EWallet.API.Controllers;

/// <summary>User profile and account management endpoints</summary>
[ApiController]
[Route("api/user")]
[Authorize]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UserController> _logger;

    public UserController(IMediator mediator, ILogger<UserController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Get the authenticated user's profile</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetProfile(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _mediator.Send(new GetUserProfileQuery(userId), ct);

        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    /// <summary>Update the authenticated user's profile</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var command = new UpdateUserProfileCommand(userId, request.FullName, request.PhoneNumber);
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
    }

    /// <summary>Change the authenticated user's password</summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var command = new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword);
        var result = await _mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            return BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("User {UserId} changed their password", userId);
        return NoContent();
    }

    /// <summary>Initiate two-factor authentication setup — sends OTP via SMS/email</summary>
    [HttpPost("enable-2fa")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnableTwoFactor(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var command = new EnableTwoFactorCommand(userId);
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? Accepted()
            : BadRequest(new ProblemDetails
            {
                Title = result.Error,
                Detail = result.ErrorCode,
                Status = StatusCodes.Status400BadRequest
            });
    }

    /// <summary>Verify OTP to complete two-factor authentication setup or high-value transfer</summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var command = new VerifyOtpCommand(userId, request.OtpCode, request.Purpose);
        var result = await _mediator.Send(command, ct);

        return result.IsSuccess
            ? Ok(new { verified = true })
            : BadRequest(new ProblemDetails
            {
                Title = "OTP Verification Failed",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });
    }

    private Guid GetCurrentUserId()
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
