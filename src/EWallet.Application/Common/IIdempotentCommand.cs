namespace EWallet.Application.Common;

/// <summary>
/// Marker interface for commands that support idempotency.
/// Commands implementing this interface must expose an IdempotencyKey property.
/// </summary>
public interface IIdempotentCommand
{
    string IdempotencyKey { get; }
}
