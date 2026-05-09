using System.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using EWallet.Infrastructure.Persistence;

namespace EWallet.Integration.Tests.Fixtures;

/// <summary>
/// Provides fast, Respawn-based database cleanup between tests.
///
/// Usage — inherit from this in test classes that need a clean DB per test:
/// <code>
/// public class MyTests : DatabaseResetFixture, IClassFixture&lt;IntegrationTestFixture&gt;
/// {
///     public MyTests(IntegrationTestFixture fixture) : base(fixture) { }
///
///     public override async Task InitializeAsync()
///     {
///         await base.InitializeAsync(); // resets DB
///     }
/// }
/// </code>
/// </summary>
public abstract class DatabaseResetFixture : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private Respawner _respawner = null!;
    private NpgsqlConnection _connection = null!;

    protected HttpClient Client => _fixture.Client;
    protected WebApplicationFactory<Program> Factory => _fixture.Factory; // re-export for convenience

    protected DatabaseResetFixture(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        // Open a dedicated connection for Respawn (it needs an open IDbConnection)
        _connection = new NpgsqlConnection(_fixture.Postgres.GetConnectionString());
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter  = DbAdapter.Postgres,
            // Preserve migration history — only wipe application tables
            TablesToIgnore = new Respawn.Graph.Table[] { new("__EFMigrationsHistory") },
        });

        await ResetDatabaseAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_connection.State == ConnectionState.Open)
            await _connection.CloseAsync();

        await _connection.DisposeAsync();
    }

    // ─── Public helpers ───────────────────────────────────────────────────────

    /// <summary>Deletes all rows from application tables using Respawn's delete ordering.</summary>
    protected async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_connection);
    }

    /// <summary>Creates a scoped <see cref="AppDbContext"/> for direct DB assertions.</summary>
    protected AppDbContext CreateDbContext() => _fixture.CreateDbContext();
}
