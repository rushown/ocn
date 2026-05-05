using EWallet.Domain.Entities;
using EWallet.Domain.Enums;
using EWallet.Infrastructure.Interfaces;
using EWallet.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EWallet.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire recurring job that finds stale pending transactions and marks them as failed.
/// Scheduled to run daily at 02:00 UTC.
/// </summary>
public sealed class TransactionCleanupJob
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TransactionCleanupJob> _logger;

    /// <summary>Initializes the cleanup job.</summary>
    public TransactionCleanupJob(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<TransactionCleanupJob> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Executes the cleanup: locates pending transactions older than 24 hours,
    /// marks each as <see cref="TransactionStatus.Failed"/>, writes an audit log entry,
    /// and persists all changes in a single batch.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        var stalePending = await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Pending && t.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (stalePending.Count == 0)
        {
            _logger.LogInformation("[TransactionCleanupJob] No stale pending transactions found.");
            return;
        }

        _logger.LogInformation(
            "[TransactionCleanupJob] Found {Count} stale pending transaction(s) to time out.", stalePending.Count);

        var auditLogs = new List<AuditLog>();

        foreach (var tx in stalePending)
        {
            var oldStatus = tx.Status.ToString();
            tx.Fail("Timed out");

            auditLogs.Add(AuditLog.Create(
                entityId: tx.Id,
                entityType: nameof(Transaction),
                action: "AutoTimeout",
                oldValues: $"{{\"Status\":\"{oldStatus}\"}}",
                newValues: $"{{\"Status\":\"Failed\",\"FailureReason\":\"Timed out\"}}",
                userId: null,          // system action
                ip: "system"));
        }

        await _context.AuditLogs.AddRangeAsync(auditLogs, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[TransactionCleanupJob] Successfully timed out {Count} transaction(s).", stalePending.Count);
    }
}

/// <summary>
/// Configures and registers Hangfire background job processing with PostgreSQL storage.
/// </summary>
public static class HangfireSetup
{
    /// <summary>
    /// Registers Hangfire with PostgreSQL storage and a two-worker server
    /// listening on <c>critical</c> and <c>default</c> queues.
    /// </summary>
    /// <param name="services">The service collection to add Hangfire to.</param>
    /// <param name="connectionString">PostgreSQL connection string for Hangfire schema storage.</param>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHangfire(config =>
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.Queues = new[] { "critical", "default" };
        });

        return services;
    }

    /// <summary>
    /// Registers the <see cref="TransactionCleanupJob"/> as a Hangfire recurring job
    /// scheduled at 02:00 UTC daily. Call this after the application has been built.
    /// </summary>
    public static void RegisterRecurringJobs()
    {
        RecurringJob.AddOrUpdate<TransactionCleanupJob>(
            recurringJobId: "transaction-cleanup",
            methodCall: job => job.ExecuteAsync(CancellationToken.None),
            cronExpression: "0 2 * * *",   // 02:00 UTC daily
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }
}
