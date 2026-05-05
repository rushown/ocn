using EWallet.BlazorClient.Models;

namespace EWallet.BlazorClient.State;

// ── Balance ─────────────────────────────────────────────────────────────────

public record FetchBalanceAction;

public record FetchBalanceSuccessAction(decimal Balance, string Currency, DateTimeOffset LastUpdated);

public record FetchBalanceFailureAction(string Error);

// ── Transactions ─────────────────────────────────────────────────────────────

public record FetchTransactionsAction(int Page = 1, int PageSize = 20, string? Type = null);

public record FetchTransactionsSuccessAction(PagedResult<TransactionDto> Result);

public record FetchTransactionsFailureAction(string Error);

// ── Transfer / Deposit / Withdraw ────────────────────────────────────────────

public record TransactionStartAction;

public record TransactionSuccessAction(TransactionDto Transaction);

public record TransactionFailureAction(string Error);

// ── Optimistic update from SignalR ────────────────────────────────────────────

public record BalanceUpdatedFromSignalRAction(decimal Balance, string Currency);

public record TransactionStatusUpdatedAction(Guid TransactionId, string Status);

// ── Reset error ───────────────────────────────────────────────────────────────

public record ClearErrorAction;
