namespace EWallet.Application.Interfaces;

public interface IIdempotencyService
{
    Task<T?> GetCachedResponseAsync<T>(string idempotencyKey, CancellationToken ct = default);
    Task StoreResponseAsync<T>(string idempotencyKey, T response, TimeSpan? ttl = null, CancellationToken ct = default);
}
