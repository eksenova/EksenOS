using System.Net;
using Eksen.Telemetry.AspNetCore.Health;
using Eksen.TestBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;

namespace Eksen.Telemetry.AspNetCore.Tests;

public class HealthCheckTests : EksenUnitTestBase
{
    [Fact]
    public async Task Health_Should_Only_Answer_On_The_Management_Port()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(endpoints => endpoints.MapEksenHealthChecks(8081));

        // Act
        using var publicLive = await host.Client.GetAsync("http://localhost/healthz/live", TestContext.Current.CancellationToken);
        using var publicReady = await host.Client.GetAsync("http://localhost:8080/healthz/ready", TestContext.Current.CancellationToken);
        using var managementLive = await host.Client.GetAsync("http://localhost:8081/healthz/live", TestContext.Current.CancellationToken);
        using var managementReady = await host.Client.GetAsync("http://localhost:8081/healthz/ready", TestContext.Current.CancellationToken);

        // Assert
        publicLive.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        publicReady.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        managementLive.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await managementLive.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
        managementReady.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_Should_Check_The_Port_The_Connection_Arrived_On_Not_Only_The_Host_Header()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(endpoints => endpoints.MapEksenHealthChecks(8081));

        // Act: the public listener with a Host header spoofing the management port
        var spoofed = await host.SendOnPortAsync("/healthz/ready", localPort: 8080, host: "anything:8081");
        var management = await host.SendOnPortAsync("/healthz/ready", localPort: 8081, host: "10.0.0.5:8081");

        // Assert
        spoofed.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        management.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Health_Should_Answer_On_The_Management_Port_Behind_A_Port_Mapping()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(endpoints => endpoints.MapEksenHealthChecks(8081));

        // Act: docker run -p 127.0.0.1:18081:8081, so the Host header names the mapped port
        var mapped = await host.SendOnPortAsync("/healthz/live", localPort: 8081, host: "127.0.0.1:18081");
        var mappedPublic = await host.SendOnPortAsync("/healthz/live", localPort: 8080, host: "127.0.0.1:18081");

        // Assert
        mapped.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        mappedPublic.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Management_Port_Isolation_Should_Hide_The_Application_On_The_Management_Port()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints =>
            {
                endpoints.MapEksenHealthChecks(8081);
                endpoints.MapGet("/api/products", () => "products");
            },
            configureApp: app => app.UseEksenManagementPortIsolation(8081));

        // Act
        var apiOnManagement = await host.SendOnPortAsync("/api/products", localPort: 8081, host: "pod:8081");
        var apiOnPublic = await host.SendOnPortAsync("/api/products", localPort: 8080, host: "shop.test");
        var healthOnManagement = await host.SendOnPortAsync("/healthz/live", localPort: 8081, host: "pod:8081");

        // Assert
        apiOnManagement.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        apiOnPublic.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        healthOnManagement.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Live_Should_Stay_Healthy_While_Ready_Reports_Failing_Checks_Without_Details()
    {
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapEksenHealthChecks(managementPort: null),
            configureServices: services => services.AddEksenHealthChecks()
                .AddCheck("database", () => HealthCheckResult.Unhealthy("connection refused to db-host:5432"), tags: [EksenHealthChecks.ReadyTag]));

        using var live = await host.Client.GetAsync("/healthz/live", TestContext.Current.CancellationToken);
        using var ready = await host.Client.GetAsync("/healthz/ready", TestContext.Current.CancellationToken);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var body = await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task Health_Requests_Should_Not_Produce_Request_Logs()
    {
        await using var host = await TelemetryTestHost.StartAsync(endpoints => endpoints.MapEksenHealthChecks(null));

        using var response = await host.Client.GetAsync("/healthz/live", TestContext.Current.CancellationToken);

        host.Logs.Events.ShouldNotContain(e => e.Category == EksenTelemetry.RequestLogCategory);
    }

    [Theory]
    [InlineData(null, "8080", null, new[] { "http://*:8080", "http://*:8081" })]
    [InlineData(null, "8080;8090", "8443", new[] { "http://*:8080", "http://*:8090", "https://*:8443", "http://*:8081" })]
    [InlineData("http://localhost:5100", "8080", null, new[] { "http://localhost:5100", "http://*:8081" })]
    [InlineData(null, null, null, new[] { "http://localhost:5000", "http://*:8081" })]
    [InlineData("http://*:8081", null, null, new[] { "http://*:8081" })]
    public void BuildListenUrls_Should_Keep_Existing_Endpoints(string? urls, string? httpPorts, string? httpsPorts, string[] expected)
    {
        EksenHealthChecks.BuildListenUrls(urls, httpPorts, httpsPorts, 8081).ShouldBe(expected);
    }

    [Fact]
    public void UseEksenManagementPort_Should_Add_The_Port_To_The_Configured_Ports()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseSetting("http_ports", "8080");

        builder.WebHost.UseEksenManagementPort(8081);

        builder.WebHost.GetSetting("urls").ShouldBe("http://*:8080;http://*:8081");
    }
}
