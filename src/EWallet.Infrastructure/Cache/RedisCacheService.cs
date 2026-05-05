using System.Text.Json;
using EWallet.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EWallet.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/>.
/// Serializes values as JSON. On <see cref="RedisConnectionException"/> the service
/// degrades gracefully (returns <c>null</c>) rather than propagating the error.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private const string KeyPrefix = "ewallet:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Initializes the cache service with the Redis connection multiplexer.</summary>
    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var fullKey = BuildKey(key);
            var value = await _db.StringGetAsync(fullKey);

            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for GET on key '{Key}'. Returning null.", key);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected cache error on GET for key '{Key}'. Returning null.", key);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var fullKey = BuildKey(key);
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _db.StringSetAsync(fullKey, json, ttl ?? DefaultTtl);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for SET on key '{Key}'. Cache miss will occur.", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected cache error on SET for key '{Key}'.", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(BuildKey(key));
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for DELETE on key '{Key}'.", key);
        }
    }

    private static string BuildKey(string key) => $"{KeyPrefix}{key}";
}
