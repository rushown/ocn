using System.Reflection;
using EWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Infrastructure.Persistence;

/// <summary>
/// Primary EF Core DbContext for the EWallet application.
/// Applies all <c>IEntityTypeConfiguration</c> implementations from this assembly
/// and dispatches domain events after each <c>SaveChanges</c>.
/// </summary>
public class AppDbContext : DbContext
{
    private readonly IPublisher? _publisher;
    private readonly ILogger<AppDbContext>? _logger;

    /// <summary>Initializes <see cref="AppDbContext"/> with options and optional MediatR publisher.</summary>
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IPublisher? publisher = null,
        ILogger<AppDbContext>? logger = null)
        : base(options)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>All registered users.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>All wallets.</summary>
    public DbSet<Wallet> Wallets => Set<Wallet>();

    /// <summary>All transactions.</summary>
    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>Immutable audit trail entries.</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Issued OTP records.</summary>
    public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Saves changes and dispatches any queued domain events through MediatR.
    /// Domain events are cleared from entities <em>before</em> the database write so that
    /// any event handler that triggers a second <c>SaveChanges</c> does not re-publish.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Collect all pending domain events from tracked aggregates
        var domainEventEntities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = domainEventEntities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Clear events from aggregates before saving to avoid re-publishing on retry
        domainEventEntities.ForEach(e => e.ClearDomainEvents());

        var result = await base.SaveChangesAsync(ct);

        // Publish events after the database write has succeeded
        if (_publisher is not null)
        {
            foreach (var domainEvent in domainEvents)
            {
                try
                {
                    await _publisher.Publish(domainEvent, ct);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "Error dispatching domain event {EventType} with id {EventId}",
                        domainEvent.GetType().Name,
                        domainEvent.EventId);
                    // Re-queue to outbox in a production system; here we swallow to not roll back the write.
                }
            }
        }

        return result;
    }
}
