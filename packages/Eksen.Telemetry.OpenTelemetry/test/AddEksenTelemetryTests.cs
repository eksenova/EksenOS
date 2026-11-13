using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.Telemetry.Background;
using Eksen.TestBase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Eksen.Telemetry.OpenTelemetry.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;

namespace Eksen.Telemetry.OpenTelemetry.Tests;

[Collection(GlobalTelemetryCollection.Name)]
public class AddEksenTelemetryTests : EksenUnitTestBase
{
    // Nothing listens there: the OTLP exporters fail fast and silently, the in-memory exporters see everything.
    private const string UnusedEndpoint = "http://127.0.0.1:9";

    [Fact]
    public void Disabled_Telemetry_Should_Register_No_OpenTelemetry_Provider()
    {
        // Arrange & Act
        using var host = BuildHost(new Dictionary<string, string?> { ["Enabled"] = "false", ["OtlpEndpoint"] = UnusedEndpoint });

        // Assert
        host.Services.GetService<TracerProvider>().ShouldBeNull();
        host.Services.GetService<MeterProvider>().ShouldBeNull();
        host.Services.GetService<LoggerProvider>().ShouldBeNull();
        host.Services.GetServices<IHostedService>().ShouldBeEmpty();
        host.Services.GetRequiredService<IOptions<EksenTelemetryOptions>>().Value.Enabled.ShouldBeFalse();
        host.Services.GetRequiredService<BackgroundJobTelemetry>().ShouldNotBeNull();
    }

    [Fact]
    public void Enabled_Telemetry_Without_An_Endpoint_Should_Stay_Disabled()
    {
        using var host = BuildHost(new Dictionary<string, string?> { ["Enabled"] = "true" });

        host.Services.GetService<TracerProvider>().ShouldBeNull();
        host.Services.GetServices<IHostedService>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Enabled_Telemetry_Should_Export_Eksen_And_Application_Spans()
    {
        // Arrange
        var exported = new List<Activity>();
        using var host = BuildHost(
            EnabledSettings(),
            services =>
            {
                services.AddEksenTelemetrySource("Custom.Registered");
                services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(exported));
            });
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var eksenSource = new ActivitySource("Eksen.EventBus");
        using var applicationSource = new ActivitySource("ImperialPlugins.Orders");
        using var registeredSource = new ActivitySource("Custom.Registered");
        using var unknownSource = new ActivitySource("Unknown.Source");

        // Act
        eksenSource.StartActivity("publish")?.Dispose();
        applicationSource.StartActivity("place-order")?.Dispose();
        registeredSource.StartActivity("registered")?.Dispose();
        unknownSource.StartActivity("ignored")?.Dispose();
        // The flush reports false because the OTLP exporters cannot reach their endpoint; the in-memory one did run.
        await EksenTelemetryFlush.ForceFlushAsync(host.Services, TimeSpan.FromSeconds(5));

        // Assert
        exported.Select(activity => activity.DisplayName).ShouldBe(["publish", "place-order", "registered"], ignoreOrder: true);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_Sampled_Remote_Parent_Is_Not_Sampled_With_Ratio_Zero()
    {
        // Arrange
        var exported = new List<Activity>();
        var settings = EnabledSettings();
        settings["SamplingRatio"] = "0";
        using var host = BuildHost(settings, services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(exported)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        using var source = new ActivitySource("ImperialPlugins.Api");

        var remoteParent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            isRemote: true);

        // Act
        using (var activity = source.StartActivity("request", ActivityKind.Server, remoteParent))
        {
            (activity?.Recorded ?? false).ShouldBeFalse();
        }

        await EksenTelemetryFlush.ForceFlushAsync(host.Services, TimeSpan.FromSeconds(5));

        // Assert
        exported.ShouldBeEmpty();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Database_Statements_Are_Removed_When_Capture_Is_Off()
    {
        var exported = new List<Activity>();
        var settings = EnabledSettings();
        settings["CaptureDbStatements"] = "false";
        using var host = BuildHost(settings, services =>
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(exported)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        using var source = new ActivitySource("Eksen.EntityFrameworkCore");

        using (var activity = source.StartActivity("SELECT"))
        {
            activity!.SetTag("db.query.text", "SELECT * FROM operators WHERE token = 'x'");
            activity.SetTag("db.system.name", "postgresql");
        }

        await EksenTelemetryFlush.ForceFlushAsync(host.Services, TimeSpan.FromSeconds(5));

        var span = exported.Single();
        span.GetTagItem("db.query.text").ShouldBeNull();
        span.GetTagItem("db.system.name").ShouldBe("postgresql");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Enabled_Telemetry_Should_Describe_The_Service_In_The_Resource()
    {
        var settings = EnabledSettings();
        settings["ServiceRole"] = "api";
        using var host = BuildHost(settings);
        await host.StartAsync(TestContext.Current.CancellationToken);

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        var attributes = resource.Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);

        attributes["service.name"].ShouldBe("backend");
        attributes["service.namespace"].ShouldBe("imperialplugins");
        attributes["service.role"].ShouldBe("api");
        attributes["deployment.environment"].ShouldBe("staging");
        attributes["service.instance.id"].ShouldBe(EksenTelemetryResource.ServiceInstanceId);
        attributes.ShouldContainKey("service.version");
        attributes.ShouldContainKey("deployment.version");
        attributes.ShouldContainKey("host.name");
        attributes["os.type"].ShouldBeOfType<string>();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Enabled_Telemetry_Should_Redact_And_Normalize_Logs_Before_Export()
    {
        // Arrange
        var capture = new CapturingLogProcessor();
        using var host = BuildHost(EnabledSettings(), services =>
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(capture)));
        await host.StartAsync(TestContext.Current.CancellationToken);
        var logger = host.Services.GetRequiredService<ILogger<AddEksenTelemetryTests>>();

        // Act
        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["eksen.user_id"] = "u-1",
                   ["password"] = "hunter2",
                   ["url"] = "https://a.test/b?token=abc"
               }))
        {
            logger.LogError(new InvalidOperationException("boom"), "Login for {password} at {url.full}", "hunter2", "/login?token=abc");
        }

        // Assert
        var record = capture.Records.Single(r => r.CategoryName == typeof(AddEksenTelemetryTests).FullName);
        record.Attributes["password"].ShouldBe("[redacted]");
        record.Attributes["url.full"].ShouldBe("/login?token=[redacted]");
        record.Attributes["exception.type"].ShouldBe("System.InvalidOperationException");
        record.Exception.ShouldBeNull();
        record.FormattedMessage.ShouldNotBeNull();
        record.FormattedMessage.ShouldNotContain("hunter2");
        var scopes = record.Scopes.ShouldNotBeNull();
        scopes["eksen.user_id"].ShouldBe("u-1");
        scopes["password"].ShouldBe("[redacted]");
        scopes["url"].ShouldBe("https://a.test/b?token=[redacted]");
        host.Services.GetServices<ILoggerProvider>().OfType<ScopeRedactingLoggerProvider>().Single()
            .Inner.ShouldBeOfType<OpenTelemetryLoggerProvider>();
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Json_Console_Should_Use_The_Redacting_Formatter()
    {
        var settings = EnabledSettings();
        settings["ConsoleJson"] = "true";
        using var host = BuildHost(settings);

        host.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue.FormatterName
            .ShouldBe(RedactingJsonConsoleFormatter.FormatterName);
        host.Services.GetServices<ConsoleFormatter>().ShouldContain(formatter => formatter is RedactingJsonConsoleFormatter);
        host.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue.IncludeScopes.ShouldBeTrue();
    }

    [Fact]
    public async Task Enabled_Telemetry_Should_Collect_Eksen_Metrics()
    {
        var exported = new List<Metric>();
        using var host = BuildHost(EnabledSettings(), services =>
            services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddInMemoryExporter(exported)));
        await host.StartAsync(TestContext.Current.CancellationToken);

        using (var iteration = host.Services.GetRequiredService<BackgroundJobTelemetry>().StartIteration("outbox"))
        {
            iteration.AddItems(2);
        }

        using var applicationMeter = new Meter("ImperialPlugins.Orders");
        applicationMeter.CreateCounter<long>("imperialplugins.orders.placed").Add(1);

        host.Services.GetRequiredService<MeterProvider>().ForceFlush(5000);

        exported.Select(metric => metric.Name).ShouldContain("eksen.background.job.runs");
        exported.Select(metric => metric.Name).ShouldContain("eksen.background.job.duration");
        exported.Select(metric => metric.Name).ShouldContain("imperialplugins.orders.placed");
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void The_Eksen_Builder_Overload_Should_Register_Telemetry()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Staging", DisableDefaults = true });
        var section = Section(EnabledSettings());

        builder.Services.AddEksen(eksen => eksen.AddTelemetry(section, builder.Environment, options => options.ServiceRole = "worker"));

        using var host = builder.Build();
        host.Services.GetService<TracerProvider>().ShouldNotBeNull();
        host.Services.GetRequiredService<IOptions<EksenTelemetryOptions>>().Value.ServiceRole.ShouldBe("worker");
    }

    [Fact]
    public async Task ForceFlush_Without_Providers_Should_Succeed()
    {
        using var host = BuildHost(new Dictionary<string, string?>());

        (await EksenTelemetryFlush.ForceFlushAsync(host.Services, TimeSpan.FromSeconds(1))).ShouldBeTrue();
    }

    private static Dictionary<string, string?> EnabledSettings()
    {
        return new Dictionary<string, string?>
        {
            ["Enabled"] = "true",
            ["ServiceName"] = "backend",
            ["ServiceNamespace"] = "imperialplugins",
            ["OtlpEndpoint"] = UnusedEndpoint,
            ["SamplingRatio"] = "1"
        };
    }

    private static IHost BuildHost(
        Dictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Staging",
            DisableDefaults = true
        });

        builder.Services.AddLogging();
        builder.Services.AddEksenTelemetry(Section(settings), builder.Environment);
        configureServices?.Invoke(builder.Services);

        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        }));

        return builder.Build();
    }

    private static IConfiguration Section(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(pair => "Telemetry:" + pair.Key, pair => pair.Value))
            .Build()
            .GetSection("Telemetry");
    }
}
