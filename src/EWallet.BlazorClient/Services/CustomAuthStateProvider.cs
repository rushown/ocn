using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace EWallet.BlazorClient.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _storage;
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public CustomAuthStateProvider(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _storage.GetItemAsync<string>("access_token");

        if (string.IsNullOrWhiteSpace(token) || IsTokenExpired(token))
            return new AuthenticationState(Anonymous);

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            ParseClaimsFromJwt(token),
            authenticationType: "jwt")));
    }

    public void NotifyAuthenticationStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var claims = ParseClaimsFromJwt(token);
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
            if (!long.TryParse(expClaim, out var exp))
                return true;

            return DateTimeOffset.FromUnixTimeSeconds(exp) <= DateTimeOffset.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return [];

        var payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Convert.FromBase64String(payload));

        if (keyValuePairs is null)
            return [];

        var claims = new List<Claim>();
        foreach (var (key, value) in keyValuePairs)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                    claims.Add(new Claim(key, item.GetString() ?? string.Empty));
            }
            else
            {
                claims.Add(new Claim(key, value.ToString()));
            }
        }

        return claims;
    }
}
