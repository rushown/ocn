using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using EWallet.Application.Contracts.Requests;
using EWallet.Application.Contracts.Responses;
using EWallet.Integration.Tests.Fixtures;
using EWallet.Integration.Tests.Helpers;

namespace EWallet.Integration.Tests.Tests;

[Collection(nameof(IntegrationTestCollection))]
public class AuthEndpointTests : DatabaseResetFixture
{
    public AuthEndpointTests(IntegrationTestFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync() => await base.InitializeAsync();
    public override async Task DisposeAsync()    => await base.DisposeAsync();

    // ─── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidData_Returns201WithAccessToken()
    {
        var request = FakeDataFactory.ValidRegisterRequest();

        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace(
            because: "registration should return a usable JWT immediately");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        var request = FakeDataFactory.ValidRegisterRequest();

        // First registration — must succeed
        var first = await Client.PostAsJsonAsync("/api/auth/register", request);
        first.EnsureSuccessStatusCode();

        // Second registration with same email — must fail
        var second = await Client.PostAsJsonAsync("/api/auth/register", request);

        second.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest,
            because: "duplicate email registrations must be rejected");
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithJwt()
    {
        var reg = FakeDataFactory.ValidRegisterRequest();
        await Client.PostAsJsonAsync("/api/auth/register", reg);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email    = reg.Email,
            Password = reg.Password,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var reg = FakeDataFactory.ValidRegisterRequest();
        await Client.PostAsJsonAsync("/api/auth/register", reg);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email    = reg.Email,
            Password = "Wr0ng@Password!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Refresh ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidRefreshToken_Returns200WithNewAccessToken()
    {
        var (_, auth, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = auth.RefreshToken,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.AccessToken.Should().NotBe(auth.AccessToken,
            because: "each refresh issues a brand-new access token");
    }

    [Fact]
    public async Task Refresh_ExpiredOrInvalidRefreshToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = "definitely-not-a-valid-token",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Logout ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidSession_ClearsRefreshTokenSoSubsequentRefreshFails()
    {
        var (authenticatedClient, auth, _) =
            await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        // Logout
        var logoutResponse = await authenticatedClient.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NoContent);

        // Attempt to refresh with the now-invalidated token
        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = auth.RefreshToken,
        });

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "logout must invalidate the refresh token in the database");
    }
}
