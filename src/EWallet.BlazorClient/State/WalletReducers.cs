using EWallet.BlazorClient.Models;
using Fluxor;

namespace EWallet.BlazorClient.State;

public static class WalletReducers
{
    [ReducerMethod]
    public static WalletState OnFetchBalance(WalletState state, FetchBalanceAction _) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static WalletState OnFetchBalanceSuccess(WalletState state, FetchBalanceSuccessAction action) =>
        state with
        {
            IsLoading = false,
            Balance = action.Balance,
            Currency = action.Currency,
            LastUpdated = action.LastUpdated,
            ErrorMessage = null
        };

    [ReducerMethod]
    public static WalletState OnFetchBalanceFailure(WalletState state, FetchBalanceFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.Error };

    [ReducerMethod]
    public static WalletState OnFetchTransactions(WalletState state, FetchTransactionsAction _) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static WalletState OnFetchTransactionsSuccess(WalletState state, FetchTransactionsSuccessAction action) =>
        state with { IsLoading = false, Transactions = action.Result };

    [ReducerMethod]
    public static WalletState OnFetchTransactionsFailure(WalletState state, FetchTransactionsFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.Error };

    [ReducerMethod]
    public static WalletState OnTransactionStart(WalletState state, TransactionStartAction _) =>
        state with { IsTransacting = true, ErrorMessage = null };

    [ReducerMethod]
    public static WalletState OnTransactionSuccess(WalletState state, TransactionSuccessAction action)
    {
        // Optimistic update: reflect outgoing transfer immediately
        var newBalance = action.Transaction.Type == "Withdrawal" || action.Transaction.Type == "Transfer"
            ? state.Balance - action.Transaction.Amount
            : state.Balance + action.Transaction.Amount;

        return state with
        {
            IsTransacting = false,
            Balance = Math.Max(0, newBalance),
            ErrorMessage = null
        };
    }

    [ReducerMethod]
    public static WalletState OnTransactionFailure(WalletState state, TransactionFailureAction action) =>
        state with { IsTransacting = false, ErrorMessage = action.Error };

    [ReducerMethod]
    public static WalletState OnBalanceUpdatedFromSignalR(WalletState state, BalanceUpdatedFromSignalRAction action) =>
        state with { Balance = action.Balance, Currency = action.Currency, LastUpdated = DateTimeOffset.UtcNow };

    [ReducerMethod]
    public static WalletState OnTransactionStatusUpdated(WalletState state, TransactionStatusUpdatedAction action)
    {
        if (state.Transactions is null) return state;

        var updatedItems = state.Transactions.Items
            .Select(t => t.Id == action.TransactionId
                ? t with { Status = action.Status }
                : t)
            .ToList()
            .AsReadOnly();

        return state with
        {
            Transactions = new PagedResult<Models.TransactionDto>(
                updatedItems,
                state.Transactions.TotalCount,
                state.Transactions.Page,
                state.Transactions.PageSize)
        };
    }

    [ReducerMethod]
    public static WalletState OnClearError(WalletState state, ClearErrorAction _) =>
        state with { ErrorMessage = null };
}
