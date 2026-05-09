using System.Net.Http.Json;
using EWallet.BlazorClient.Models;
using System.Text.Json;

namespace EWallet.BlazorClient.Services;

public class WalletService : IWalletService
{
    private readonly HttpClient _http;
    public string? LastError { get; private set; }

    public WalletService(HttpClient http)
    {
        _http = http;
    }

    public async Task<WalletBalanceDto?> GetBalanceAsync()
    {
        LastError = null;
        try
        {
            var response = await _http.GetAsync("/api/wallet/balance");
            if (!response.IsSuccessStatusCode)
            {
                LastError = await ReadErrorAsync(response, "Failed to load balance.");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<WalletBalanceDto>();
        }
        catch
        {
            LastError = "Network error while loading balance.";
            return null;
        }
    }

    public async Task<TransactionDto?> DepositAsync(DepositRequest request, string idempotencyKey)
    {
        LastError = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/deposit");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                LastError = await ReadErrorAsync(response, "Deposit failed.");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TransactionDto>();
        }
        catch
        {
            LastError = "Network error while processing deposit.";
            return null;
        }
    }

    public async Task<TransactionDto?> WithdrawAsync(WithdrawRequest request, string idempotencyKey)
    {
        LastError = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/withdraw");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                LastError = await ReadErrorAsync(response, "Withdrawal failed.");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TransactionDto>();
        }
        catch
        {
            LastError = "Network error while processing withdrawal.";
            return null;
        }
    }

    public async Task<TransactionDto?> TransferAsync(TransferRequest request, string idempotencyKey)
    {
        LastError = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/transfer");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                LastError = await ReadErrorAsync(response, "Transfer failed.");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TransactionDto>();
        }
        catch
        {
            LastError = "Network error while processing transfer.";
            return null;
        }
    }

    public async Task<PagedResult<TransactionDto>?> GetTransactionsAsync(
        int page = 1,
        int pageSize = 20,
        string? type = null)
    {
        LastError = null;
        try
        {
            var url = $"/api/wallet/transactions?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(type))
                url += $"&type={type}";

            return await _http.GetFromJsonAsync<PagedResult<TransactionDto>>(url);
        }
        catch
        {
            LastError = "Failed to load transactions.";
            return null;
        }
    }

    public async Task<WalletLookupDto?> LookupWalletAsync(Guid walletId)
    {
        LastError = null;
        try
        {
            var response = await _http.GetAsync($"/api/wallet/lookup/{walletId}");
            if (!response.IsSuccessStatusCode)
            {
                LastError = await ReadErrorAsync(response, "Wallet lookup failed.");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<WalletLookupDto>();
        }
        catch
        {
            LastError = "Network error while looking up wallet.";
            return null;
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            var json = await JsonDocument.ParseAsync(stream);

            if (json.RootElement.TryGetProperty("detail", out var detail) &&
                !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString()!;
            }

            if (json.RootElement.TryGetProperty("title", out var title) &&
                !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }
        }
        catch
        {
            // Ignore parsing errors and use fallback message.
        }

        return fallback;
    }
}
