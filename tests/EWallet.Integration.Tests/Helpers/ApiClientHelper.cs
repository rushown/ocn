using System.Net.Http.Headers;
using System.Net.Http.Json;
using EWallet.Application.Contracts.Requests;
using EWallet.Application.Contracts.Responses;

namespace EWallet.Integration.Tests.Helpers;

/// <summary>
/// Convenience wrappers that drive the auth flow programmatically so individual
/// tests can get a ready-to-use authenticated <see cref="HttpClient"/> in one call.
/// </summary>
public static class ApiClientHelper
{
    // ─── Auth flow ────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new user, logs them in, attaches the Bearer token, and returns
    /// both the client and the full <see cref="AuthResponse"/>.
    /// </summary>
    public static async Task<(HttpClient client, AuthResponse auth, RegisterRequest credentials)>
        CreateAuthenticatedClientAsync(HttpClient client, RegisterRequest? request = null)
    {
        var reg = request ?? FakeDataFactory.ValidRegisterRequest();

        // Register
        var regResponse = await client.PostAsJsonAsync("/api/auth/register", reg);
        regResponse.EnsureSuccessStatusCode();

        // Login to get tokens (registration may or may not return them directly)
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email    = reg.Email,
            Password = reg.Password,
        });
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>()
                   ?? throw new InvalidOperationException("Login did not return an AuthResponse.");

        // Attach bearer token for all subsequent requests
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return (client, auth, reg);
    }

    /// <summary>
    /// Creates a second independent authenticated client using a *new* <see cref="HttpClient"/>
    /// instance — useful for multi-user transfer tests.
    /// </summary>
    public static async Task<(HttpClient client, AuthResponse auth, RegisterRequest credentials)>
        CreateSecondAuthenticatedClientAsync(
            System.Net.Http.HttpMessageHandler handler,
            RegisterRequest? request = null)
    {
        var secondClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return await CreateAuthenticatedClientAsync(secondClient, request);
    }

    // ─── Wallet helpers ───────────────────────────────────────────────────────

    /// <summary>Deposits funds and returns the parsed response body.</summary>
    public static async Task<DepositResponse?> DepositAsync(
        HttpClient client,
        decimal amount,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/deposit")
        {
            Content = JsonContent.Create(FakeDataFactory.ValidDepositRequest(amount)),
        };

        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DepositResponse>();
    }

    /// <summary>Fetches the authenticated user's wallet balance.</summary>
    public static async Task<WalletBalanceResponse?> GetBalanceAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/wallet/balance");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WalletBalanceResponse>();
    }

    /// <summary>Removes the Authorization header to simulate an unauthenticated request.</summary>
    public static void RemoveAuthHeader(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = null;
}
