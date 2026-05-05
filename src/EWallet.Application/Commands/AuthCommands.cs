using EWallet.Application.Common;
using EWallet.Application.DTOs;
using MediatR;

namespace EWallet.Application.Commands;

public record RegisterCommand(
    string Email,
    string PhoneNumber,
    string FullName,
    string Password,
    string ConfirmPassword) : IRequest<Result<AuthResponse>>;

public record LoginCommand(
    string Email,
    string Password) : IRequest<Result<AuthResponse>>;

public record RefreshTokenCommand(
    string RefreshToken) : IRequest<Result<AuthResponse>>;

public record LogoutCommand(
    Guid UserId) : IRequest<Result>;
