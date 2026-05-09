using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using EWallet.Infrastructure.Persistence;

namespace EWallet.Integration.Tests.Fixtures;

/// <summary>
/// Spins up real PostgreSQL + Redis containers once per test collection,
/// applies EF migrations, and exposes a configured <see cref="HttpClient"/>.
///
/// Shared across the collection via <c>[Collection(nameof(IntegrationTestCollection))]</c>
/// so containers start only once per test run.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    // ─── Containers ──────────────────────────────────────────────────────────

    public PostgreSqlContainer Postgres { get; private set; } = null!;
    public RedisContainer Redis { get; private set; } = null!;

    // ─── Public surface ───────────────────────────────────────────────────────

    public HttpClient Client { get; private set; } = null!;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    // ─── IAsyncLifetime ───────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        // Integration tests require a working Docker-compatible engine (Docker or Podman socket).
        // Keep `dotnet test` usable on machines without containers by opting in explicitly.
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
            return;

        Postgres = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("ewallet_test")
            .WithUsername("test")
            .WithPassword("test")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        Redis = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();

        // Start infrastructure in parallel to save CI time
        await Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync());

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = Postgres.GetConnectionString(),
                        ["ConnectionStrings:Redis"]             = Redis.GetConnectionString(),
                        // Long enough key for HMAC-SHA256 (>= 256 bits)
                        ["Jwt:Secret"] = "integration-test-secret-key-256-bits-long-enough!!",
                        ["Jwt:ExpiryMinutes"]        = "60",
                        ["Jwt:RefreshExpiryDays"]    = "7",
                        // Disable rate limiting in most tests (RateLimitTests override this)
                        ["RateLimit:Enabled"]        = "false",
                    });
                });
            });

        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        // Run EF migrations against the real Postgres container
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
        if (Postgres is not null && Redis is not null)
            await Task.WhenAll(Postgres.DisposeAsync().AsTask(), Redis.DisposeAsync().AsTask());
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Creates a new scoped <see cref="AppDbContext"/> for direct DB assertions.</summary>
    public AppDbContext CreateDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

/// <summary>
/// xUnit collection definition — all test classes sharing this collection
/// receive the same <see cref="IntegrationTestFixture"/> instance.
/// </summary>
[CollectionDefinition(nameof(IntegrationTestCollection))]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture> { }
