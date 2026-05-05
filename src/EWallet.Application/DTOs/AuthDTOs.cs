namespace EWallet.Application.DTOs;

public record RegisterRequest(
    string Email,
    string PhoneNumber,
    string FullName,
    string Password,
    string ConfirmPassword);

public record LoginRequest(
    string Email,
    string Password);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string Email);

public record RefreshTokenRequest(
    string RefreshToken);
