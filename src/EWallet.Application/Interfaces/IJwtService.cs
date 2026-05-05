using System.Security.Claims;
using EWallet.Domain.Entities;

namespace EWallet.Application.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}
