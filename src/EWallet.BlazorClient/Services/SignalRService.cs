using Microsoft.AspNetCore.SignalR.Client;

namespace EWallet.BlazorClient.Services;

public class SignalRService : ISignalRService
{
    private HubConnection? _hub;
    private readonly IConfiguration _configuration;

    public event Action<decimal, string>? OnBalanceUpdated;
    public event Action<Guid, string>? OnTransactionUpdated;

    private string _apiBaseUrl;

    public SignalRService(IConfiguration configuration)
    {
        _configuration = configuration;
        _apiBaseUrl = configuration["ApiBaseUrl"] ?? "http://localhost:5000";
    }

    public async Task StartAsync(string accessToken)
    {
        if (_hub is not null)
            await StopAsync();

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_apiBaseUrl}/hubs/wallet", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) })
            .Build();

        _hub.On<decimal, string>("BalanceUpdated", (amount, currency) =>
            OnBalanceUpdated?.Invoke(amount, currency));

        _hub.On<Guid, string>("TransactionUpdated", (txId, status) =>
            OnTransactionUpdated?.Invoke(txId, status));

        _hub.Closed += async (error) =>
        {
            if (error is not null)
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _hub.StartAsync();
            }
        };

        try
        {
            await _hub.StartAsync();
        }
        catch
        {
            // Connection failed; AutoReconnect will retry
        }
    }

    public async Task StopAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
