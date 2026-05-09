using Blazored.LocalStorage;
using EWallet.BlazorClient.Models;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EWallet.BlazorClient.Services;

public class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly ILocalStorageService _storage;
    private readonly IHttpClientFactory _clientFactory;
    private readonly NavigationManager _navigation;

    public AuthorizationMessageHandler(
        ILocalStorageService storage,
        IHttpClientFactory clientFactory,
        NavigationManager navigation)
    {
        _storage = storage;
        _clientFactory = clientFactory;
        _navigation = navigation;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _storage.GetItemAsync<string>("access_token", cancellationToken);

        if (!string.IsNullOrEmpty(token))
        {
            // Proactively refresh if < 2 minutes remaining
            if (IsTokenNearExpiry(token))
            {
                var refreshed = await TryRefreshAsync(cancellationToken);
                if (refreshed is not null)
                    token = refreshed;
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // On 401, try one token refresh
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var newToken = await TryRefreshAsync(cancellationToken);
            if (newToken is not null)
            {
                // Clone and retry
                var retryRequest = await CloneRequestAsync(request);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                response = await base.SendAsync(retryRequest, cancellationToken);
            }
            else
            {
                // Refresh failed — redirect to login
                _navigation.NavigateTo("/login", forceLoad: false);
            }
        }

        return response;
    }

    private async Task<string?> TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _clientFactory.CreateClient("EWalletApiPublic");
            var response = await client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshRequest(),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await _storage.RemoveItemAsync("access_token", cancellationToken);
                return null;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(
                cancellationToken: cancellationToken);

            if (auth is null) return null;

            await _storage.SetItemAsync("access_token", auth.AccessToken, cancellationToken);
            return auth.AccessToken;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTokenNearExpiry(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
            var json = JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(payload));

            if (json.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp))
                return DateTimeOffset.FromUnixTimeSeconds(exp) < DateTimeOffset.UtcNow.AddMinutes(2);

            return true;
        }
        catch
        {
            return true;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (original.Content is not null)
        {
            var content = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
