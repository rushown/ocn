using EWallet.BlazorClient.Models;

namespace EWallet.BlazorClient.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetAccessTokenAsync();
    Task<UserDto?> GetCurrentUserAsync();
}

public interface IWalletService
{
    Task<WalletBalanceDto?> GetBalanceAsync();
    Task<TransactionDto?> DepositAsync(DepositRequest request, string idempotencyKey);
    Task<TransactionDto?> WithdrawAsync(WithdrawRequest request, string idempotencyKey);
    Task<TransactionDto?> TransferAsync(TransferRequest request, string idempotencyKey);
    Task<PagedResult<TransactionDto>?> GetTransactionsAsync(int page = 1, int pageSize = 20, string? type = null);
    Task<WalletLookupDto?> LookupWalletAsync(Guid walletId);
}

public interface ISignalRService : IAsyncDisposable
{
    event Action<decimal, string>? OnBalanceUpdated;
    event Action<Guid, string>? OnTransactionUpdated;
    Task StartAsync(string accessToken);
    Task StopAsync();
}
