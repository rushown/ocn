using EWallet.Application.Common;
using EWallet.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EWallet.Application.Behaviors;

public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public IdempotencyBehavior(
        IIdempotencyService idempotency,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _idempotency = idempotency;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only intercept commands that opt in to idempotency
        if (request is not IIdempotentCommand idempotentCommand)
            return await next();

        var key = idempotentCommand.IdempotencyKey;

        var cached = await _idempotency.GetCachedResponseAsync<TResponse>(key, cancellationToken);
        if (cached is not null)
        {
            _logger.LogInformation("Idempotency cache hit for key {Key}", key);
            return cached;
        }

        var response = await next();

        // Only cache successful responses
        if (response is not null)
            await _idempotency.StoreResponseAsync(key, response, DefaultTtl, cancellationToken);

        return response;
    }
}
