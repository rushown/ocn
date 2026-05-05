using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using EWallet.BlazorClient.Models;

namespace EWallet.BlazorClient.Services;

public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILocalStorageService _storage;

    public AuthService(IHttpClientFactory httpClientFactory, ILocalStorageService storage)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
    }

    private HttpClient PublicClient => _httpClientFactory.CreateClient("EWalletApiPublic");

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await PublicClient.PostAsJsonAsync("/api/auth/login", request);
            if (!response.IsSuccessStatusCode)
                return null;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null)
                return null;

            await PersistTokensAsync(auth);
            return auth;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await PublicClient.PostAsJsonAsync("/api/auth/register", request);
            if (!response.IsSuccessStatusCode)
                return null;

            // Auto-login after registration
            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null)
                return null;

            await PersistTokensAsync(auth);
            return auth;
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (token is not null)
            {
                var http = _httpClientFactory.CreateClient("EWalletApi");
                await http.PostAsync("/api/auth/logout", null);
            }
        }
        finally
        {
            await _storage.RemoveItemAsync("access_token");
            await _storage.RemoveItemAsync("refresh_token");
            await _storage.RemoveItemAsync("current_user");
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _storage.GetItemAsync<string>("access_token");
        if (string.IsNullOrEmpty(token))
            return false;

        return !IsTokenExpired(token);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var token = await _storage.GetItemAsync<string>("access_token");
        if (string.IsNullOrEmpty(token) || IsTokenExpired(token))
            return null;

        return token;
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        var cached = await _storage.GetItemAsync<UserDto>("current_user");
        if (cached is not null)
            return cached;

        try
        {
            var http = _httpClientFactory.CreateClient("EWalletApi");
            return await http.GetFromJsonAsync<UserDto>("/api/auth/me");
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResponse?> RefreshTokenAsync()
    {
        var refreshToken = await _storage.GetItemAsync<string>("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
            return null;

        try
        {
            var response = await PublicClient.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshRequest(refreshToken));

            if (!response.IsSuccessStatusCode)
            {
                await _storage.RemoveItemAsync("access_token");
                await _storage.RemoveItemAsync("refresh_token");
                return null;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null)
                return null;

            await PersistTokensAsync(auth);
            return auth;
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistTokensAsync(AuthResponse auth)
    {
        await _storage.SetItemAsync("access_token", auth.AccessToken);
        await _storage.SetItemAsync("refresh_token", auth.RefreshToken);
        await _storage.SetItemAsync("current_user", auth.User);
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return true;

            // Pad base64 string
            var payload = parts[1];
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var bytes = Convert.FromBase64String(payload);
            var json = JsonSerializer.Deserialize<JsonElement>(bytes);

            if (json.TryGetProperty("exp", out var expElement) &&
                expElement.TryGetInt64(out var exp))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
                // Consider expired if less than 2 minutes remaining
                return expiry < DateTimeOffset.UtcNow.AddMinutes(2);
            }

            return true;
        }
        catch
        {
            return true;
        }
    }
}
