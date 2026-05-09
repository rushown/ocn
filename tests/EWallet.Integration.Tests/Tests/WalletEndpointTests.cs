using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using EWallet.Domain.Enums;
using EWallet.Integration.Tests.Fixtures;
using EWallet.Integration.Tests.Helpers;
using EWallet.Application.DTOs;

namespace EWallet.Integration.Tests.Tests;

[Collection(nameof(IntegrationTestCollection))]
public class WalletEndpointTests : DatabaseResetFixture
{
    public WalletEndpointTests(IntegrationTestFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            return;
        await base.InitializeAsync();
    }

    public override async Task DisposeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            return;
        await base.DisposeAsync();
    }

    // ─── Balance ─────────────────────────────────────────────────────────────

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task GetBalance_Authenticated_Returns200WithBalance()
    {
        var (client, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        var response = await client.GetAsync("/api/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BalanceDto>();
        body.Should().NotBeNull();
        body!.Balance.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task GetBalance_Unauthenticated_Returns401()
    {
        // Remove any existing auth header
        ApiClientHelper.RemoveAuthHeader(Client);

        var response = await Client.GetAsync("/api/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Deposit ─────────────────────────────────────────────────────────────

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Deposit_ValidRequestWithIdempotencyKey_Returns200AndCreatesTransaction()
    {
        var (client, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);
        var idempotencyKey  = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/deposit")
        {
            Content = JsonContent.Create(FakeDataFactory.ValidDepositRequest(100m)),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify a Completed transaction record was persisted
        await using var db = CreateDbContext();
        var tx = await db.Transactions
            .FirstOrDefaultAsync(t => t.Status == TransactionStatus.Completed);

        tx.Should().NotBeNull(
            because: "a successful deposit must persist a Completed transaction");
    }

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Deposit_MissingIdempotencyKeyHeader_Returns400()
    {
        var (client, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        // No Idempotency-Key header
        var response = await client.PostAsJsonAsync(
            "/api/wallet/deposit", FakeDataFactory.ValidDepositRequest(100m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "the Idempotency-Key header is required for all deposit requests");
    }

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Deposit_SameIdempotencyKeyTwice_ReturnsSameResponseBothTimes()
    {
        var (client, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);
        var idempotencyKey  = Guid.NewGuid().ToString();

        async Task<HttpResponseMessage> SendDeposit()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/deposit")
            {
                Content = JsonContent.Create(FakeDataFactory.ValidDepositRequest(50m)),
            };
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            return await client.SendAsync(req);
        }

        var first  = await SendDeposit();
        var second = await SendDeposit();

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "replaying the same idempotency key must return 200, not an error");

        var firstBody  = await first.Content.ReadFromJsonAsync<TransactionDto>();
        var secondBody = await second.Content.ReadFromJsonAsync<TransactionDto>();

        secondBody!.Id.Should().Be(firstBody!.Id,
            because: "idempotent replay must return the original transaction, not create a new one");

        // Balance must only have been credited once
        var balance = await ApiClientHelper.GetBalanceAsync(client);
        balance!.Balance.Should().Be(50m);
    }

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Deposit_ZeroAmount_Returns400()
    {
        var (client, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/deposit")
        {
            Content = JsonContent.Create(FakeDataFactory.ValidDepositRequest(0m)),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Transfer scenarios are covered elsewhere; this suite focuses on auth + basic wallet operations.
}
