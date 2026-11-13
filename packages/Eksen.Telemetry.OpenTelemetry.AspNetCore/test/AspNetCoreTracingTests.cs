using System.Diagnostics;
using System.Net;
using Eksen.TestBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Shouldly;

namespace Eksen.Telemetry.OpenTelemetry.AspNetCore.Tests;

public class AspNetCoreTracingTests : EksenUnitTestBase
{
    [Fact]
    public async Task Server_Spans_Should_Carry_Scrubbed_Paths_And_Redacted_Queries()
    {
        // Arrange
        var exported = new List<Activity>();
        using var host = await StartAsync(exported);

        // Act
        using var response = await host.GetTestClient().GetAsync(
            "/0b3f8c3e-7d55-4c35-9c2f-6c1c1c0b6a11/LicenseChecker/check?page=2&apikey=abc",
            TestContext.Current.CancellationToken);
        await FlushAsync(host);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var span = exported.Single();
        span.Kind.ShouldBe(ActivityKind.Server);
        span.GetTagItem("url.path").ShouldBe("/{license}/LicenseChecker/check");
        span.GetTagItem("url.query").ShouldBe("?page=2&apikey=[redacted]");
        span.GetTagItem("http.route").ShouldBe("/{key}/LicenseChecker/check");
        span.TagObjects.Select(tag => tag.Value?.ToString() ?? string.Empty)
            .ShouldNotContain(value => value.Contains("0b3f8c3e", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Oversized_Queries_Should_Be_Cut_On_Server_Spans()
    {
        // Arrange
        var exported = new List<Activity>();
        using var host = await StartAsync(exported);
        var padding = new string('a', 10_000);

        // Act
        using var response = await host.GetTestClient().GetAsync(
            "/0b3f8c3e-7d55-4c35-9c2f-6c1c1c0b6a11/LicenseChecker/check?token=abc&q=" + padding,
            TestContext.Current.CancellationToken);
        await FlushAsync(host);

        // Assert
        var query = exported.Single().GetTagItem("url.query").ShouldBeOfType<string>();
        // Cut to the limit before redaction; masking token=abc as token=[redacted] then adds a few characters.
        query.Length.ShouldBeLessThan(EksenOpenTelemetryAspNetCoreExtensions.MaxUrlTagLength + 64);
        query.ShouldStartWith("?token=[redacted]&q=aaa");
        query.ShouldEndWith("...[truncated]");
    }

    [Fact]
    public async Task Excluded_Paths_Should_Not_Be_Traced()
    {
        var exported = new List<Activity>();
        using var host = await StartAsync(exported);

        using var response = await host.GetTestClient().GetAsync("/healthz/live", TestContext.Current.CancellationToken);
        await FlushAsync(host);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        exported.ShouldBeEmpty();
    }

    [Fact]
    public async Task Server_Errors_Should_Set_The_Error_Type()
    {
        var exported = new List<Activity>();
        using var host = await StartAsync(exported);

        using var response = await host.GetTestClient().GetAsync("/unavailable", TestContext.Current.CancellationToken);
        await FlushAsync(host);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        exported.Single().GetTagItem("error.type").ShouldBe("503");
    }

    [Fact]
    public void Without_Enabled_Telemetry_No_Tracer_Provider_Is_Built()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEksenAspNetCoreTelemetry();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        provider.GetService<TracerProvider>().ShouldBeNull();
    }

    private static async Task<IHost> StartAsync(List<Activity> exported)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:Enabled"] = "true",
                ["Telemetry:ServiceName"] = "backend",
                ["Telemetry:OtlpEndpoint"] = "http://127.0.0.1:9"
            })
            .Build();

        var host = new HostBuilder()
            .UseEnvironment("Staging")
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices((context, services) =>
                {
                    services.AddRouting();
                    services.AddEksenTelemetry(configuration.GetSection("Telemetry"), context.HostingEnvironment);
                    services.AddEksenAspNetCoreTelemetry();
                    services.AddEksenUrlPathScrubRule(
                        "^/[0-9a-fA-F-]{36}/(LicenseChecker|LicenseValidation|VersionCheck)/",
                        "/{license}/$1/");
                    services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(exported));
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/{key}/LicenseChecker/check", (string key) => "ok");
                        endpoints.MapGet("/healthz/live", () => "Healthy");
                        endpoints.MapGet("/unavailable", () => Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
                    });
                });
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private static Task FlushAsync(IHost host)
    {
        return EksenTelemetryFlush.ForceFlushAsync(host.Services, TimeSpan.FromSeconds(5));
    }
}
