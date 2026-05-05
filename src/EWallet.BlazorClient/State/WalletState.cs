using EWallet.BlazorClient.Models;
using Fluxor;

namespace EWallet.BlazorClient.State;

[FeatureState]
public record WalletState
{
    public decimal Balance { get; init; } = 0m;
    public string Currency { get; init; } = "USD";
    public bool IsLoading { get; init; } = false;
    public bool IsTransacting { get; init; } = false;
    public string? ErrorMessage { get; init; }
    public PagedResult<TransactionDto>? Transactions { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
}
