using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;

namespace Eksen.Scalar.Tests;

/// <summary>
/// Spins up a real, minimal <see cref="WebApplication"/> on a loopback Kestrel port with a Scalar
/// reference mapped, so the rendered HTML (and therefore the injected plugin head content) can be
/// asserted over HTTP exactly as a browser would receive it.
/// </summary>
internal sealed class ScalarTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ScalarTestHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<ScalarTestHost> StartAsync(Action<ScalarOptions> configure, string routePrefix = "/scalar")
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddOpenApi();
        builder.Services.AddRouting();

        var app = builder.Build();

        app.MapOpenApi();
        app.MapScalarApiReference(routePrefix, configure);

        await app.StartAsync();

        var address = app.Urls.First();
        var client = new HttpClient { BaseAddress = new Uri(address) };

        return new ScalarTestHost(app, client);
    }

    /// <summary>
    /// Fetches the rendered Scalar HTML page (following the trailing-slash redirect).
    /// </summary>
    public Task<string> GetReferenceHtmlAsync(string routePrefix = "/scalar")
    {
        return Client.GetStringAsync(routePrefix);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
