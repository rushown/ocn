using System.Text.Json.Serialization;

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
    [property: JsonIgnore] string RefreshToken,
    DateTime ExpiresAt,
    Guid UserId,
    string Email);

// Refresh tokens are handled via HttpOnly cookies in the API layer.
