using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using EWallet.API.Models;
using EWallet.Application.DTOs;
using EWallet.Integration.Tests.Fixtures;
using EWallet.Integration.Tests.Helpers;

namespace EWallet.Integration.Tests.Tests;

[Collection(nameof(IntegrationTestCollection))]
public class AuthEndpointTests : DatabaseResetFixture
{
    public AuthEndpointTests(IntegrationTestFixture fixture) : base(fixture) { }

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

    // ─── Register ─────────────────────────────────────────────────────────────

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
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

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        var request = FakeDataFactory.ValidRegisterRequest();

        // First registration — must succeed
        var first = await Client.PostAsJsonAsync("/api/auth/register", request);
        first.EnsureSuccessStatusCode();

        // Second registration with same email — must fail
        var second = await Client.PostAsJsonAsync("/api/auth/register", request);

        second.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Login_ValidCredentials_Returns200WithJwt()
    {
        var reg = FakeDataFactory.ValidRegisterRequest();
        await Client.PostAsJsonAsync("/api/auth/register", reg);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new EWallet.API.Models.LoginRequest(reg.Email, reg.Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.Contains("refreshToken=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Login_WrongPassword_Returns401()
    {
        var reg = FakeDataFactory.ValidRegisterRequest();
        await Client.PostAsJsonAsync("/api/auth/register", reg);

        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new EWallet.API.Models.LoginRequest(reg.Email, "Wr0ng@Password!"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Refresh ─────────────────────────────────────────────────────────────

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Refresh_ValidRefreshToken_Returns200WithNewAccessToken()
    {
        var (_, auth, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.AccessToken.Should().NotBe(auth.AccessToken,
            because: "each refresh issues a brand-new access token");
    }

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Refresh_ExpiredOrInvalidRefreshToken_Returns401()
    {
        var response = await Client.PostAsync("/api/auth/refresh", JsonContent.Create(new RefreshTokenRequest()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─── Logout ──────────────────────────────────────────────────────────────

    [Fact(Skip = "Requires containers. Run with RUN_INTEGRATION_TESTS=true and a working Docker/Podman socket.")]
    public async Task Logout_ValidSession_ClearsRefreshTokenSoSubsequentRefreshFails()
    {
        var (authenticatedClient, auth, _) = await ApiClientHelper.CreateAuthenticatedClientAsync(Client);

        // Logout
        var logoutResponse = await authenticatedClient.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.NoContent);

        // Attempt to refresh with the now-invalidated cookie
        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest());

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "logout must invalidate the refresh token in the database");
    }
}
