using System.Net;
using System.Net.Http.Json;
using Eksen.Cloudflare;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eksen.Cloudflare.Tests;

/// <summary>
/// Drives a real ASP.NET Core pipeline through TestServer. The peer address (the TCP source that
/// nginx or IIS would see) is injected by a middleware in front of <c>UseCloudflare</c>, so these
/// tests exercise the genuine trust decision rather than a mocked one.
/// </summary>
public sealed class CloudflareMiddlewareE2eTests
{
    private const string PeerHeaderName = "X-Test-Peer";

    private const string CloudflarePeer = "203.0.113.10";

    private const string NonCloudflarePeer = "66.249.72.237";

    private const string RealClient = "198.51.100.7";

    private sealed record ProbeResult(string? RemoteIpAddress, string Scheme, bool IsHttps);

    private sealed class StubRangeProvider(CloudflareIpRanges ranges) : ICloudflareIpRangeProvider
    {
        public CloudflareIpRanges Current { get; } = ranges;

        public bool IsLoaded
        {
            get { return !Current.IsEmpty; }
        }

        public Task<CloudflareIpRanges> RefreshAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Current);
        }
    }

    private static TestServer CreateServer(Action<EksenCloudflareOptions>? configureOptions = null)
    {
        var builder = new HostBuilder().ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();

            webHost.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

                services.AddSingleton<ICloudflareIpRangeProvider>(
                    new StubRangeProvider(FakeCloudflareIpRangeSource.SampleRanges));

                services.AddOptions<EksenCloudflareOptions>().Configure(options =>
                {
                    configureOptions?.Invoke(options);
                });
            });

            webHost.Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    var peer = context.Request.Headers[PeerHeaderName].ToString();

                    if (!string.IsNullOrEmpty(peer))
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                    }

                    await next(context);
                });

                app.UseCloudflare();

                app.Run(async context =>
                {
                    await context.Response.WriteAsJsonAsync(new ProbeResult(
                        context.Connection.RemoteIpAddress?.ToString(),
                        context.Request.Scheme,
                        context.Request.IsHttps));
                });
            });
        });

        var host = builder.Start();
        return host.GetTestServer();
    }

    private static async Task<ProbeResult> ProbeAsync(
        TestServer server,
        string peer,
        string? connectingIp = RealClient,
        string? forwardedProto = "https")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(PeerHeaderName, peer);

        if (connectingIp is not null)
        {
            request.Headers.Add(CloudflareDefaults.ConnectingIpHeaderName, connectingIp);
        }

        if (forwardedProto is not null)
        {
            request.Headers.Add(CloudflareDefaults.ForwardedProtoHeaderName, forwardedProto);
        }

        using var client = server.CreateClient();
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProbeResult>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Request_From_Cloudflare_Peer_Should_Restore_Client_Ip_And_Https_Scheme()
    {
        using var server = CreateServer();

        var result = await ProbeAsync(server, CloudflarePeer);

        Assert.Equal(RealClient, result.RemoteIpAddress);
        Assert.Equal("https", result.Scheme);
        Assert.True(result.IsHttps);
    }

    // The whole point of the trusted-range gate: a direct-to-origin client must not be able to
    // forge either header.
    [Fact]
    public async Task Request_From_Non_Cloudflare_Peer_Should_Ignore_Both_Headers()
    {
        using var server = CreateServer();

        var result = await ProbeAsync(server, NonCloudflarePeer);

        Assert.Equal(NonCloudflarePeer, result.RemoteIpAddress);
        Assert.Equal("http", result.Scheme);
        Assert.False(result.IsHttps);
    }

    [Fact]
    public async Task Cloudflare_Peer_Sending_IPv6_Client_Should_Be_Restored()
    {
        using var server = CreateServer();

        var result = await ProbeAsync(server, CloudflarePeer, connectingIp: "2a02:ff0:220:d6f6::1");

        Assert.Equal("2a02:ff0:220:d6f6::1", result.RemoteIpAddress);
        Assert.Equal("https", result.Scheme);
    }

    [Fact]
    public async Task Cloudflare_Peer_Without_Headers_Should_Leave_Request_Untouched()
    {
        using var server = CreateServer();

        var result = await ProbeAsync(server, CloudflarePeer, connectingIp: null, forwardedProto: null);

        Assert.Equal(CloudflarePeer, result.RemoteIpAddress);
        Assert.Equal("http", result.Scheme);
    }

    [Fact]
    public async Task Malformed_Connecting_Ip_Should_Be_Ignored()
    {
        using var server = CreateServer();

        var result = await ProbeAsync(server, CloudflarePeer, connectingIp: "not-an-ip");

        Assert.Equal(CloudflarePeer, result.RemoteIpAddress);
        Assert.Equal("https", result.Scheme);
    }

    [Fact]
    public async Task Forwarded_Proto_Should_Be_Ignorable_By_Configuration()
    {
        using var server = CreateServer(options => options.RestoreForwardedProto = false);

        var result = await ProbeAsync(server, CloudflarePeer);

        Assert.Equal(RealClient, result.RemoteIpAddress);
        Assert.Equal("http", result.Scheme);
    }

    [Fact]
    public async Task Connecting_Ip_Should_Be_Ignorable_By_Configuration()
    {
        using var server = CreateServer(options => options.RestoreConnectingIp = false);

        var result = await ProbeAsync(server, CloudflarePeer);

        Assert.Equal(CloudflarePeer, result.RemoteIpAddress);
        Assert.Equal("https", result.Scheme);
    }

    [Fact]
    public async Task Original_Peer_Address_Should_Remain_Available_For_Diagnostics()
    {
        var captured = new List<string?>();

        var builder = new HostBuilder().ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();

            webHost.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
                services.AddSingleton<ICloudflareIpRangeProvider>(
                    new StubRangeProvider(FakeCloudflareIpRangeSource.SampleRanges));
                services.AddOptions<EksenCloudflareOptions>();
            });

            webHost.Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(CloudflarePeer);
                    await next(context);
                });

                app.UseCloudflare();

                app.Run(context =>
                {
                    captured.Add(
                        context.Items[CloudflareDefaults.OriginalRemoteIpAddressItemKey]?.ToString());
                    return Task.CompletedTask;
                });
            });
        });

        using var host = builder.Start();
        using var server = host.GetTestServer();
        using var client = server.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(CloudflareDefaults.ConnectingIpHeaderName, RealClient);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.Equal(CloudflarePeer, Assert.Single(captured));
    }
}
