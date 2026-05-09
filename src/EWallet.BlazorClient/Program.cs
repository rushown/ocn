using Blazored.LocalStorage;
using EWallet.BlazorClient;
using EWallet.BlazorClient.Services;
using Fluxor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Determine API base URL
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? builder.HostEnvironment.BaseAddress.Replace(":5001", ":5000");

// Register AuthorizationMessageHandler (DelegatingHandler)
builder.Services.AddScoped<AuthorizationMessageHandler>();

// API HttpClient with auth handler
builder.Services.AddHttpClient("EWalletApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddHttpMessageHandler<AuthorizationMessageHandler>();

// Named plain client for auth endpoints (no token needed)
builder.Services.AddHttpClient("EWalletApiPublic", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// HttpClient factory accessor for services
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("EWalletApi"));

// Local storage
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ISignalRService, SignalRService>();

// Fluxor state management
builder.Services.AddFluxor(options =>
{
    options.ScanAssemblies(typeof(Program).Assembly);
    options.UseReduxDevTools();
});

// MudBlazor
builder.Services.AddMudServices();

await builder.Build().RunAsync();
