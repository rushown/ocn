using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using EWallet.Integration.Tests.Fixtures;
using EWallet.Integration.Tests.Helpers;

namespace EWallet.Integration.Tests.Tests;

/// <summary>
/// Verifies that the API enforces its per-user rate limit on wallet endpoints.
///
/// NOTE: These tests use a *separate* <see cref="WebApplicationFactory"/> instance
/// with rate limiting explicitly ENABLED (the shared fixture disables it so other
/// tests run at full speed).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class RateLimitTests : DatabaseResetFixture
{
    /// <summary>
    /// Factory override that enables rate limiting for this test class only.
    /// Configured to allow 10 requests per minute on wallet endpoints.
    /// </summary>
    private readonly WebApplicationFactory<Program> _rateLimitedFactory;
    private readonly HttpClient _rateLimitedClient;

    public RateLimitTests(IntegrationTestFixture fixture) : base(fixture)
    {
        _rateLimitedFactory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Enabled"]              = "true",
                    ["RateLimit:WalletRequestsPerMin"] = "10",
                });
            });
        });

        _rateLimitedClient = _rateLimitedFactory.CreateClient();
    }

    public override async Task InitializeAsync() => await base.InitializeAsync();

    public override async Task DisposeAsync()
    {
        _rateLimitedClient.Dispose();
        await _rateLimitedFactory.DisposeAsync();
        await base.DisposeAsync();
    }

    // ─── Rate limit enforcement ──────────────────────────────────────────────

    [Fact]
    public async Task WalletEndpoint_After10Requests_11thRequestReturns429()
    {
        // Authenticate against the rate-limited factory
        var (client, _, _) =
            await ApiClientHelper.CreateAuthenticatedClientAsync(_rateLimitedClient);

        var responses = new List<HttpResponseMessage>();

        // Send 11 rapid-fire GET /balance requests
        for (var i = 0; i < 11; i++)
        {
            responses.Add(await client.GetAsync("/api/wallet/balance"));
        }

        // First 10 must succeed
        responses.Take(10).Should().OnlyContain(
            r => r.StatusCode == HttpStatusCode.OK,
            because: "the rate limit allows 10 requests per minute");

        // 11th must be throttled
        responses[10].StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            because: "the 11th request in the same window must be rejected with 429");
    }

    [Fact]
    public async Task WalletEndpoint_DifferentUsers_RateLimitAppliedPerUser()
    {
        // Two separate users — each should get their own 10-request window
        var clientB = _rateLimitedFactory.CreateClient();

        var (clientA, _, _) =
            await ApiClientHelper.CreateAuthenticatedClientAsync(_rateLimitedClient);
        var (authedB, _, _) =
            await ApiClientHelper.CreateAuthenticatedClientAsync(clientB);

        // Send 10 requests as user A
        for (var i = 0; i < 10; i++)
            await clientA.GetAsync("/api/wallet/balance");

        // User B's first request should NOT be rate-limited
        var firstBResponse = await authedB.GetAsync("/api/wallet/balance");

        firstBResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "rate limits are scoped per authenticated user, not globally");
    }
}
