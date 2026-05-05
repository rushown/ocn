using System.Net.Http.Json;
using EWallet.BlazorClient.Models;

namespace EWallet.BlazorClient.Services;

public class WalletService : IWalletService
{
    private readonly HttpClient _http;

    public WalletService(HttpClient http)
    {
        _http = http;
    }

    public async Task<WalletBalanceDto?> GetBalanceAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<WalletBalanceDto>("/api/wallet/balance");
        }
        catch
        {
            return null;
        }
    }

    public async Task<TransactionDto?> DepositAsync(DepositRequest request, string idempotencyKey)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/deposit");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<TransactionDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<TransactionDto?> WithdrawAsync(WithdrawRequest request, string idempotencyKey)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/withdraw");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<TransactionDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<TransactionDto?> TransferAsync(TransferRequest request, string idempotencyKey)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/wallet/transfer");
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<TransactionDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<PagedResult<TransactionDto>?> GetTransactionsAsync(
        int page = 1,
        int pageSize = 20,
        string? type = null)
    {
        try
        {
            var url = $"/api/wallet/transactions?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(type))
                url += $"&type={type}";

            return await _http.GetFromJsonAsync<PagedResult<TransactionDto>>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<WalletLookupDto?> LookupWalletAsync(Guid walletId)
    {
        try
        {
            return await _http.GetFromJsonAsync<WalletLookupDto>($"/api/wallet/lookup/{walletId}");
        }
        catch
        {
            return null;
        }
    }
}
