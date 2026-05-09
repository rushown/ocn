using System.Text.Json;
using EWallet.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EWallet.Infrastructure.Cache;

/// <summary>
/// Redis-backed idempotency store.
/// Uses <c>SET NX</c> (set-if-not-exists) to guarantee that only the first caller
/// registers a key — racing duplicate requests will see the cached response.
/// </summary>
public sealed class RedisIdempotencyService : IIdempotencyService
{
    private const string KeyPrefix = "idempotency:";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IDatabase _db;
    private readonly ILogger<RedisIdempotencyService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Initializes the idempotency service with the Redis connection multiplexer.</summary>
    public RedisIdempotencyService(IConnectionMultiplexer redis, ILogger<RedisIdempotencyService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns <c>true</c> when the key did not previously exist (first call — process the request).
    /// Returns <c>false</c> when the key already existed (duplicate — return cached response).
    /// </remarks>
    public async Task StoreResponseAsync<T>(string idempotencyKey, T response, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var fullKey = BuildKey(idempotencyKey);
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await _db.StringSetAsync(fullKey, json, ttl ?? Ttl);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis connection failed during idempotency SET for key '{Key}'.",
                idempotencyKey);
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetCachedResponseAsync<T>(string idempotencyKey, CancellationToken ct = default)
    {
        try
        {
            var fullKey = BuildKey(idempotencyKey);
            var value = await _db.StringGetAsync(fullKey);

            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex,
                "Redis connection failed during idempotency GET for key '{Key}'.",
                idempotencyKey);
            return default;
        }
    }

    private static string BuildKey(string key) => $"{KeyPrefix}{key}";
}
