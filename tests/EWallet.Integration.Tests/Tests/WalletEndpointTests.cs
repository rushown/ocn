using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using EWallet.Application.Contracts.Requests;
using EWallet.Application.Contracts.Responses;
using EWallet.Domain.Enums;
using EWallet.Integration.Tests.Fixtures;
using EWallet.Integration.Tests.Helpers;

namespace EWallet.Integration.Tests.Tests;

[Collection(nameof(IntegrationTestCollection))]
public class WalletEndpointTests : DatabaseResetFixture
{
    public WalletEndpointTests(IntegrationTestFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync() => await base.InitializeAsync();
    public override async Task DisposeAsync()    => await base.DisposeAsync();

    // ─── Balance ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalance_Authenticated_Returns200WithBalance()
    {
        var (client, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        var response = await client.GetAsync("/api/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<WalletBalanceResponse>();
        body.Should().NotBeNull();
        body!.Balance.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public async Task GetBalance_Unauthenticated_Returns401()
    {
        // Remove any existing auth header
        ApiClientHelper.RemoveAuthHeader(Client);

        var response = await Client.GetAsync("/api/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Deposit ─────────────────────────────────────────────────────────────

    [Fact]
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify a Completed transaction record was persisted
        await using var db = CreateDbContext();
        var tx = await db.Transactions
            .FirstOrDefaultAsync(t => t.Status == TransactionStatus.Completed);

        tx.Should().NotBeNull(
            because: "a successful deposit must persist a Completed transaction");
    }

    [Fact]
    public async Task Deposit_MissingIdempotencyKeyHeader_Returns400()
    {
        var (client, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        // No Idempotency-Key header
        var response = await client.PostAsJsonAsync(
            "/api/wallet/deposit", FakeDataFactory.ValidDepositRequest(100m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "the Idempotency-Key header is required for all deposit requests");
    }

    [Fact]
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

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "replaying the same idempotency key must return 200, not an error");

        var firstBody  = await first.Content.ReadFromJsonAsync<DepositResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<DepositResponse>();

        secondBody!.TransactionId.Should().Be(firstBody!.TransactionId,
            because: "idempotent replay must return the original transaction, not create a new one");

        // Balance must only have been credited once
        var balance = await ApiClientHelper.GetBalanceAsync(client);
        balance!.Balance.Should().Be(50m);
    }

    [Fact]
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

    // ─── Transfer – happy path ────────────────────────────────────────────────

    [Fact]
    public async Task Transfer_HappyPath_UpdatesBothBalancesCorrectly()
    {
        // Step 1: Register user A and user B
        var (clientA, authA, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        // User B needs their own HttpClient (separate bearer token)
        var clientB = Factory.CreateClient();
        var (_, authB, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(clientB);

        // Step 2: Deposit $500 to user A
        await ApiClientHelper.DepositAsync(clientA, 500m, Guid.NewGuid().ToString());

        // Fetch user B's wallet ID from their profile
        var profileB = await clientB.GetFromJsonAsync<UserProfileResponse>("/api/users/me");

        // Step 3: Transfer $200 from A → B
        var transferReq = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/transfer")
        {
            Content = JsonContent.Create(
                FakeDataFactory.ValidTransferRequest(profileB!.WalletId, 200m)),
        };
        transferReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var transferResponse = await clientA.SendAsync(transferReq);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Assert balances
        var balanceA = await ApiClientHelper.GetBalanceAsync(clientA);
        var balanceB = await ApiClientHelper.GetBalanceAsync(clientB);

        balanceA!.Balance.Should().Be(300m, because: "A deposited $500 and sent $200");
        balanceB!.Balance.Should().Be(200m, because: "B received $200 from A");
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_Returns400WithInsufficientFundsCode()
    {
        var (clientA, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);
        var clientB         = Factory.CreateClient();
        var (_, _, _)       = await ApiClientHelper.CreateAuthenticatedClientAsync(clientB);

        var profileB = await clientB.GetFromJsonAsync<UserProfileResponse>("/api/users/me");

        // Attempt to transfer $500 with $0 balance
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/transfer")
        {
            Content = JsonContent.Create(
                FakeDataFactory.ValidTransferRequest(profileB!.WalletId, 500m)),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await clientA.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsWithCode>();
        problem!.ErrorCode.Should().Be("INSUFFICIENT_FUNDS");
    }

    // ─── Transfer – concurrent / idempotent ──────────────────────────────────

    [Fact]
    public async Task Transfer_SameIdempotencyKeyFiredConcurrently_OnlyOneTransferOccurs()
    {
        var (clientA, _, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);
        var clientB         = Factory.CreateClient();
        await ApiClientHelper.CreateAuthenticatedClientAsync(clientB);
        var profileB        = await clientB.GetFromJsonAsync<UserProfileResponse>("/api/users/me");

        // Give A exactly $100
        await ApiClientHelper.DepositAsync(clientA, 100m, Guid.NewGuid().ToString());

        var sharedKey = Guid.NewGuid().ToString();

        // Fire two concurrent requests with the SAME idempotency key for $80
        HttpRequestMessage BuildRequest() => new(HttpMethod.Post, "/api/wallet/transfer")
        {
            Content = JsonContent.Create(
                FakeDataFactory.ValidTransferRequest(profileB!.WalletId, 80m)
                with { IdempotencyKey = sharedKey }),
        };

        var tasks = new[]
        {
            clientA.SendAsync(BuildRequest()),
            clientA.SendAsync(BuildRequest()),
        };

        var responses = await Task.WhenAll(tasks);

        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.OK,
            because: "both requests carry the same idempotency key so both should return 200");

        // Balance must reflect exactly one $80 debit
        var balance = await ApiClientHelper.GetBalanceAsync(clientA);
        balance!.Balance.Should().Be(20m,
            because: "idempotent replay means only one $80 debit occurs against the $100 balance");
    }
}
