using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using EWallet.Integration.Tests.Fixtures;
using EWallet.Integration.Tests.Helpers;

namespace EWallet.Integration.Tests.Tests;

/// <summary>
/// Stress-tests the transfer handler under concurrent load.
/// Uses real Postgres row-level locking / optimistic concurrency to verify
/// that the balance never goes negative when parallel debits race.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ConcurrencyTests : DatabaseResetFixture
{
    public ConcurrencyTests(IntegrationTestFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync() => await base.InitializeAsync();
    public override async Task DisposeAsync()    => await base.DisposeAsync();

    // ─── Parallel withdrawals ────────────────────────────────────────────────

    /// <summary>
    /// Given a wallet with $100, fire 10 concurrent withdrawal requests for $20 each.
    /// Exactly 5 should succeed (5 × $20 = $100), the other 5 must fail with
    /// INSUFFICIENT_FUNDS.  The final balance must be $0, never negative.
    /// </summary>
    [Fact]
    public async Task ParallelWithdrawals_ExactlyHalfSucceed_BalanceNeverNegative()
    {
        // Arrange — fund a single wallet with exactly $100
        var (clientA, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);
        await ApiClientHelper.DepositAsync(clientA, 100m, Guid.NewGuid().ToString());

        // Need a receiver for the "withdrawals" (modelled as transfers to a dummy wallet)
        var receiverClient = Factory.CreateClient();
        await ApiClientHelper.CreateAuthenticatedClientAsync(receiverClient);
        var receiverProfile = await receiverClient
            .GetFromJsonAsync<Application.Contracts.Responses.UserProfileResponse>("/api/users/me");

        // Act — 10 concurrent transfer requests, each for $20
        var tasks = Enumerable.Range(0, 10).Select(_ =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/transfer")
            {
                Content = System.Net.Http.Json.JsonContent.Create(
                    FakeDataFactory.ValidTransferRequest(receiverProfile!.WalletId, 20m)),
            };
            // Each request has a UNIQUE idempotency key → no de-duplication, pure concurrency
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return clientA.SendAsync(req);
        });

        var responses = await Task.WhenAll(tasks);

        // Assert — categorise outcomes
        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var failed    = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        succeeded.Should().Be(5, because: "$100 / $20 = exactly 5 transfers can succeed");
        failed.Should().Be(5,    because: "the remaining 5 must be rejected with insufficient funds");

        // Final balance must be exactly $0, never below
        var balance = await ApiClientHelper.GetBalanceAsync(clientA);
        balance!.Balance.Should().Be(0m,
            because: "concurrent debits must never drive the balance negative");
    }

    // ─── Double-spend prevention ─────────────────────────────────────────────

    /// <summary>
    /// Two simultaneous transfers each claiming the entire balance.
    /// Only one should succeed; the other must fail.
    /// </summary>
    [Fact]
    public async Task DoubleSpend_BothTransfersTryToClaimEntireBalance_OnlyOneSucceeds()
    {
        var (clientA, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);
        await ApiClientHelper.DepositAsync(clientA, 200m, Guid.NewGuid().ToString());

        var receiverClient = Factory.CreateClient();
        await ApiClientHelper.CreateAuthenticatedClientAsync(receiverClient);
        var receiverProfile = await receiverClient
            .GetFromJsonAsync<Application.Contracts.Responses.UserProfileResponse>("/api/users/me");

        // Two separate transfers both for $200 with unique idempotency keys
        var tasks = Enumerable.Range(0, 2).Select(_ =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/transfer")
            {
                Content = System.Net.Http.Json.JsonContent.Create(
                    FakeDataFactory.ValidTransferRequest(receiverProfile!.WalletId, 200m)),
            };
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return clientA.SendAsync(req);
        });

        var responses = await Task.WhenAll(tasks);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        succeeded.Should().Be(1, because: "the balance covers only one full transfer");

        var balance = await ApiClientHelper.GetBalanceAsync(clientA);
        balance!.Balance.Should().Be(0m);
        balance.Balance.Should().BeGreaterThanOrEqualTo(0m,
            because: "balance must never go negative regardless of concurrency");
    }
}
