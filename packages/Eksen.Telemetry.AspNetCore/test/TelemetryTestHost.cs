using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eksen.Telemetry.AspNetCore.Tests;

/// <summary>
/// An in-memory web host with the request telemetry pipeline, an ASP.NET Core activity listener and a log capture.
/// </summary>
public sealed class TelemetryTestHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly ActivityListener _listener;

    private TelemetryTestHost(IHost host, ActivityListener listener, CapturingLoggerProvider logs)
    {
        _host = host;
        _listener = listener;
        Logs = logs;
        Client = host.GetTestClient();
    }

    public CapturingLoggerProvider Logs { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services
    {
        get { return _host.Services; }
    }

    public static async Task<TelemetryTestHost> StartAsync(
        Action<IEndpointRouteBuilder> mapEndpoints,
        Dictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configureServices = null,
        Action<IApplicationBuilder>? configureApp = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        var logs = new CapturingLoggerProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection((settings ?? []).ToDictionary(pair => "Telemetry:" + pair.Key, pair => pair.Value))
            .Build();

        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddLogging(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
                    services.AddRouting();
                    services.AddEksenTelemetryAspNetCore(configuration.GetSection("Telemetry"));
                    services.AddEksenHealthChecks();
                    configureServices?.Invoke(services);
                });
                web.Configure(app =>
                {
                    app.UseEksenTelemetry();
                    configureApp?.Invoke(app);
                    app.UseRouting();
                    app.UseEksenRouteEnrichment();
                    app.UseEksenRequestEnrichment();
                    app.UseEksenInternalEndpointGuard();
                    app.UseEndpoints(endpoints => mapEndpoints(endpoints));
                });
            })
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        return new TelemetryTestHost(host, listener, logs);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync(TestContext.Current.CancellationToken);
        _host.Dispose();
        _listener.Dispose();
    }

    public TestServer Server
    {
        get { return _host.GetTestServer(); }
    }

    /// <summary>
    /// Sends a GET as if it arrived on <paramref name="localPort"/> with the given <c>Host</c> header, which
    /// <see cref="Client"/> cannot do (the test server leaves the local port at 0).
    /// </summary>
    public Task<HttpContext> SendOnPortAsync(string path, int localPort, string host)
    {
        return Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Scheme = "http";
            context.Request.Host = new HostString(host);
            context.Request.Path = path;
            context.Connection.LocalPort = localPort;
        }, TestContext.Current.CancellationToken);
    }

    public CapturedLogEvent CompletionEvent()
    {
        return Logs.Events.Single(e => e.Category == EksenTelemetry.RequestLogCategory && e.Message.StartsWith("HTTP ", StringComparison.Ordinal));
    }
}
