using System.Diagnostics;
using System.Net;
using Eksen.Telemetry.AspNetCore;
using Eksen.Telemetry.OpenTelemetry.Logging;
using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Eksen.Telemetry.OpenTelemetry.Tests;

/// <summary>
/// The full logging pipeline (OpenTelemetry export with the redacting processor and scope provider, and the
/// redacting JSON console) on a web host, fed headers built to make backtracking redaction quadratic.
/// </summary>
[Collection(GlobalTelemetryCollection.Name)]
public class AdversarialRequestTests : EksenUnitTestBase
{
    // Nothing listens there: the OTLP exporters fail fast and silently.
    private const string UnusedEndpoint = "http://127.0.0.1:9";

    [Theory]
    [InlineData("<token")]
    [InlineData("\"token")]
    [InlineData("?a")]
    public async Task A_30_KB_Adversarial_Header_Should_Not_Stall_The_Request(string unit)
    {
        // Arrange
        using var host = await StartAsync();
        var client = host.GetTestClient();
        using var warmUp = await client.GetAsync("/log", TestContext.Current.CancellationToken);

        // 30 KB stays under Kestrel's default 32 KB request header limit.
        var junk = string.Concat(Enumerable.Repeat(unit, 30 * 1024 / unit.Length));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/log");
        request.Headers.TryAddWithoutValidation("X-Junk", junk);
        request.Headers.TryAddWithoutValidation("User-Agent", junk);

        // Act
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Scopes_Redacted_When_They_Were_Built_Should_Not_Be_Redacted_Again()
    {
        // Arrange
        var fields = new RedactedLogFields();
        fields.Set("http.request.header.x-junk", "{\"password\":\"hidden\"}");
        fields.Set("password", 42);

        // Act
        var scope = RedactingExternalScopeProvider.RedactScope(fields);

        // Assert
        scope.ShouldBeSameAs(fields);
        fields["http.request.header.x-junk"].ShouldBe("{\"password\":\"[redacted]\"}");
        fields["password"].ShouldBe("[redacted]");
    }

    private static async Task<IHost> StartAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:Enabled"] = "true",
                ["Telemetry:ServiceName"] = "adversarial-tests",
                ["Telemetry:OtlpEndpoint"] = UnusedEndpoint,
                ["Telemetry:ConsoleJson"] = "true",
                ["Telemetry:ConsoleMinimumLevel"] = "Information"
            })
            .Build();

        var host = new HostBuilder()
            .UseEnvironment("Staging")
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices((context, services) =>
                {
                    services.AddLogging();
                    services.AddRouting();
                    services.AddEksenTelemetry(configuration.GetSection("Telemetry"), context.HostingEnvironment);
                    services.AddEksenTelemetryAspNetCore(configuration.GetSection("Telemetry"));
                });
                web.Configure(app =>
                {
                    app.UseEksenTelemetry();
                    app.UseRouting();
                    app.UseEksenRouteEnrichment();
                    app.UseEksenRequestEnrichment();
                    app.UseEndpoints(endpoints => endpoints.MapGet("/log", (ILogger<AdversarialRequestTests> logger) =>
                    {
                        for (var line = 0; line < 5; line++)
                        {
                            logger.LogInformation("Inside the handler {Line}", line);
                        }

                        return Results.Ok();
                    }));
                });
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }
}
