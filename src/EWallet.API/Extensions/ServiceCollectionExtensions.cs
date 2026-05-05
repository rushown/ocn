using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

namespace EWallet.API.Extensions;

/// <summary>
/// Extension methods to keep Program.cs clean and focused on composition.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers JWT Bearer authentication with SignalR query-string token support.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        var jwtSettings = config.GetSection("Jwt");
        var secret = jwtSettings["Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured. Set Jwt__Secret environment variable.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Support SignalR JWT supplied via query-string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Registers Swagger/OpenAPI with JWT security definition.
    /// </summary>
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "EWallet API",
                Version = "v1",
                Description = "Digital wallet REST API with real-time SignalR notifications"
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Enter: Bearer {your JWT token}",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Include XML doc comments
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                c.IncludeXmlComments(xmlPath);
        });

        return services;
    }

    /// <summary>
    /// Registers rate limiters:
    ///   - WalletOps: 10 requests/minute per IP (wallet mutations)
    ///   - Auth: 5 requests/minute per IP (login/register)
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("WalletOps", opt =>
            {
                opt.PermitLimit = 10;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("Auth", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.Headers["Retry-After"] = "60";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    title = "Too Many Requests",
                    detail = "Rate limit exceeded. Please wait before retrying.",
                    status = 429
                }, ct);
            };
        });

        return services;
    }
}
