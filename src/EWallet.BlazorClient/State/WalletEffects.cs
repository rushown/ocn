using EWallet.BlazorClient.Services;
using Fluxor;

namespace EWallet.BlazorClient.State;

public class WalletEffects
{
    private readonly IWalletService _walletService;

    public WalletEffects(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [EffectMethod]
    public async Task HandleFetchBalance(FetchBalanceAction _, IDispatcher dispatcher)
    {
        var result = await _walletService.GetBalanceAsync();
        if (result is not null)
            dispatcher.Dispatch(new FetchBalanceSuccessAction(result.Balance, result.Currency, result.LastUpdated));
        else
            dispatcher.Dispatch(new FetchBalanceFailureAction("Failed to load balance. Please try again."));
    }

    [EffectMethod]
    public async Task HandleFetchTransactions(FetchTransactionsAction action, IDispatcher dispatcher)
    {
        var result = await _walletService.GetTransactionsAsync(action.Page, action.PageSize, action.Type);
        if (result is not null)
            dispatcher.Dispatch(new FetchTransactionsSuccessAction(result));
        else
            dispatcher.Dispatch(new FetchTransactionsFailureAction("Failed to load transactions."));
    }
}
