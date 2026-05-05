using EWallet.Domain.Enums;

namespace EWallet.Application.DTOs;

public record UserProfileDto(
    Guid Id,
    string Email,
    string PhoneNumber,
    string FullName,
    KycLevel KycLevel,
    bool IsTwoFactorEnabled,
    DateTime CreatedAt);

public record UpdateProfileRequest(
    string FullName,
    string PhoneNumber);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

public record VerifyOtpRequest(
    Guid UserId,
    string Code,
    string Purpose);
