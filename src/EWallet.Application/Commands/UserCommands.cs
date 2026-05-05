using EWallet.Application.Common;
using EWallet.Application.DTOs;
using MediatR;

namespace EWallet.Application.Commands;

public record UpdateUserProfileCommand(
    Guid UserId,
    string FullName,
    string PhoneNumber) : IRequest<Result<UserProfileDto>>;

public record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<Result>;

/// <summary>
/// Returns the TOTP secret string for the authenticator app.
/// </summary>
public record EnableTwoFactorCommand(
    Guid UserId) : IRequest<Result<string>>;

public record VerifyOtpCommand(
    Guid UserId,
    string Code,
    string Purpose) : IRequest<Result<bool>>;
