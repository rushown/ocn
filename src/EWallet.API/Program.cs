using EWallet.API.Extensions;
using EWallet.API.Filters;
using EWallet.API.Hubs;
using EWallet.API.Middleware;
using EWallet.API.Services;
using EWallet.Application;
using EWallet.Application.Common.Interfaces;
using EWallet.Infrastructure;
using EWallet.Infrastructure.BackgroundJobs;
using Hangfire;
using Serilog;

// ─── Bootstrap logger (before DI is available) ───────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EWallet API");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ─────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .Enrich.FromLogContext()
           .Enrich.WithCorrelationId()
           .WriteTo.Console()
           .WriteTo.Seq(ctx.Configuration["Serilog:SeqUrl"] ?? "http://localhost:5341"));

    // ─── Application & Infrastructure layers ─────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ─── Auth ─────────────────────────────────────────────────────────────────
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    });

    // ─── API layer ────────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerWithJwt();
    builder.Services.AddRateLimiting();

    // Exception handling (ASP.NET 8 IExceptionHandler)
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // ─── SignalR with Redis backplane ─────────────────────────────────────────
    var redisConn = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string is required.");

    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redisConn, opts =>
        {
            opts.Configuration.ChannelPrefix = RedisChannel.Literal("ewallet");
        });

    // Real-time notification service
    builder.Services.AddScoped<IWalletNotificationService, WalletNotificationService>();

    // ─── CORS ─────────────────────────────────────────────────────────────────
    var allowedOrigin = builder.Configuration["AllowedOrigins"] ?? "http://localhost:5001";
    builder.Services.AddCors(options =>
        options.AddPolicy("BlazorClient", policy =>
            policy.WithOrigins(allowedOrigin)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    // ─── Health checks ────────────────────────────────────────────────────────
    var dbConn = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is required.");

    builder.Services.AddHealthChecks()
        .AddNpgSql(dbConn, name: "postgres", tags: new[] { "db", "ready" })
        .AddRedis(redisConn, name: "redis", tags: new[] { "cache", "ready" });

    // ─── Filters ─────────────────────────────────────────────────────────────
    builder.Services.AddScoped<IdempotencyFilter>();

    // ─── Build ────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Middleware pipeline ──────────────────────────────────────────────────
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseRequestLogging();       // custom correlation ID + security headers
    app.UseHttpsRedirection();
    app.UseCors("BlazorClient");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "EWallet API v1");
            c.RoutePrefix = string.Empty; // Swagger at root in dev
        });
    }

    // ─── Endpoints ───────────────────────────────────────────────────────────
    app.MapControllers();
    app.MapHub<WalletHub>("/hubs/wallet");
    app.MapHealthChecks("/health");

    // Hangfire dashboard — protected in non-dev environments
    app.MapHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = app.Environment.IsDevelopment()
            ? new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
            : new[] { new HangfireAdminRoleFilter() }
    });

    // ─── Recurring jobs ───────────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        RecurringJob.AddOrUpdate<TransactionCleanupJob>(
            "transaction-cleanup",
            job => job.Execute(),
            Cron.Daily(2)); // 02:00 UTC daily
    }

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "EWallet API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
