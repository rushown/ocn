using EWallet.Domain.Interfaces;
using EWallet.Infrastructure.BackgroundJobs;
using EWallet.Infrastructure.Cache;
using EWallet.Infrastructure.Interfaces;
using EWallet.Infrastructure.PaymentGateway;
using EWallet.Infrastructure.Persistence;
using EWallet.Infrastructure.Repositories;
using EWallet.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace EWallet.Infrastructure;

/// <summary>
/// Extension methods for registering all Infrastructure-layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core, Redis, Hangfire, repositories, and all infrastructure services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration (connection strings, JWT settings, etc.).</param>
    /// <param name="environment">Hosting environment — controls whether the fake payment gateway is used.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        // ── EF Core + PostgreSQL ──────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // ── Repositories + Unit of Work ───────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // ── Redis ─────────────────────────────────────────────────────────────
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string is not configured.");

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IIdempotencyService, RedisIdempotencyService>();

        // ── Application Services ──────────────────────────────────────────────
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<INotificationService, NotificationService>();

        // ── Current User (requires IHttpContextAccessor) ──────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // ── Payment Gateway ───────────────────────────────────────────────────
        // Default to fake gateway unless explicitly disabled.
        // This keeps local/staging environments bootable while still allowing strict production enforcement.
        var useFakeGateway = configuration.GetValue("Payments:UseFakeGateway", true);
        if (environment?.IsProduction() == true && useFakeGateway)
        {
            throw new InvalidOperationException(
                "Production misconfiguration: Payments:UseFakeGateway is true. Refusing to start with FakePaymentGateway.");
        }
        if (useFakeGateway)
        {
#pragma warning disable CS0618 // FakePaymentGateway is intentionally Obsolete
            services.AddScoped<IPaymentGateway, FakePaymentGateway>();
#pragma warning restore CS0618
        }
        else
        {
            // TODO: Register real payment gateway implementation here
            // services.AddScoped<IPaymentGateway, StripePaymentGateway>();
            throw new NotImplementedException(
                "Payments:UseFakeGateway is false but no real IPaymentGateway implementation is registered.");
        }

        // ── Hangfire ──────────────────────────────────────────────────────────
        var hangfireEnabled = configuration.GetValue("Hangfire:Enabled", false);
        if (hangfireEnabled)
        {
            services.AddHangfireServices(
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection string is not configured."));
        }

        return services;
    }
}
