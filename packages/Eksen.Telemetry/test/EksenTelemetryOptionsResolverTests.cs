using Eksen.TestBase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Shouldly;

namespace Eksen.Telemetry.Tests;

public class EksenTelemetryOptionsResolverTests : EksenUnitTestBase
{
    [Fact]
    public void Defaults_Should_Match_The_Documented_Values()
    {
        var options = EksenTelemetryOptionsResolver.Resolve(Section([]), Environment("Production"), NoVariables);

        options.Enabled.ShouldBeFalse();
        options.OtlpProtocol.ShouldBe("grpc");
        options.SamplingRatio.ShouldBe(1.0);
        options.RecordExceptions.ShouldBeTrue();
        options.CaptureRequestBodies.ShouldBeFalse();
        options.MaxRequestBodyBytes.ShouldBe(32768);
        options.CaptureOutboundBodies.ShouldBeFalse();
        options.ExcludedPaths.ShouldBe(["/healthz", "/openapi", "/scalar", "/favicon.ico"]);
        options.ConsoleJson.ShouldBe(true);
        options.ConsoleMinimumLevel.ShouldBe(LogLevel.Warning);
        options.CaptureDbStatements.ShouldBeTrue();
        options.UnitOfWorkSpans.ShouldBeFalse();
        options.ManagementPort.ShouldBeNull();
        options.ServiceName.ShouldBe("TestApp");
        options.IsExportEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Configured_Values_Should_Win_And_Lists_Should_Replace_Defaults()
    {
        var options = EksenTelemetryOptionsResolver.Resolve(Section(new Dictionary<string, string?>
        {
            ["Enabled"] = "true",
            ["ServiceName"] = "backend",
            ["OtlpEndpoint"] = "http://alloy:4317",
            ["OtlpProtocol"] = "http/protobuf",
            ["SamplingRatio"] = "0.25",
            ["ExcludedPaths:0"] = "/internal",
            ["RedactedHeaders:0"] = "X-Signature",
            ["ManagementPort"] = "8081",
            ["ConsoleMinimumLevel"] = "Error"
        }), Environment("Development"), Variables(new Dictionary<string, string>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://ignored:4317",
            ["OTEL_SERVICE_NAME"] = "ignored",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc"
        }));

        options.ServiceName.ShouldBe("backend");
        options.OtlpEndpoint.ShouldBe("http://alloy:4317");
        options.IsHttpProtobuf.ShouldBeTrue();
        options.SamplingRatio.ShouldBe(0.25);
        options.ExcludedPaths.ShouldBe(["/internal"]);
        options.RedactedHeaders.ShouldBe(["X-Signature"]);
        options.ManagementPort.ShouldBe(8081);
        options.ConsoleMinimumLevel.ShouldBe(LogLevel.Error);
        options.ConsoleJson.ShouldBe(false);
        options.IsExportEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Otel_Environment_Variables_Should_Fill_Empty_Values()
    {
        var options = EksenTelemetryOptionsResolver.Resolve(Section(new Dictionary<string, string?> { ["Enabled"] = "true" }),
            Environment("Production"),
            Variables(new Dictionary<string, string>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://alloy-gateway:4318",
                ["OTEL_EXPORTER_OTLP_HEADERS"] = "Authorization=Bearer%20abc,x-env=staging",
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=worker,service.namespace=imperialplugins,k8s.pod.name=w-1"
            }));

        options.OtlpEndpoint.ShouldBe("http://alloy-gateway:4318");
        options.ParseOtlpHeaders().ShouldBe([
            new KeyValuePair<string, string>("Authorization", "Bearer abc"),
            new KeyValuePair<string, string>("x-env", "staging")
        ]);
        options.IsHttpProtobuf.ShouldBeTrue();
        options.ServiceName.ShouldBe("worker");
        options.ServiceNamespace.ShouldBe("imperialplugins");
        options.IsExportEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Otel_Service_Name_Should_Win_Over_Resource_Attributes()
    {
        var options = EksenTelemetryOptionsResolver.Resolve(Section([]), null, Variables(new Dictionary<string, string>
        {
            ["OTEL_SERVICE_NAME"] = "bot",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=worker"
        }));

        options.ServiceName.ShouldBe("bot");
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    public void An_Out_Of_Range_Sampling_Ratio_Should_Be_Rejected(string ratio)
    {
        Should.Throw<InvalidOperationException>(() => EksenTelemetryOptionsResolver.Resolve(
            Section(new Dictionary<string, string?> { ["SamplingRatio"] = ratio }), null, NoVariables));
    }

    [Fact]
    public void IsExcludedPath_Should_Match_Prefixes_Case_Insensitively()
    {
        var options = new EksenTelemetryOptions();

        options.IsExcludedPath("/healthz/ready").ShouldBeTrue();
        options.IsExcludedPath("/Scalar/v1").ShouldBeTrue();
        options.IsExcludedPath("/api/products").ShouldBeFalse();
        options.IsExcludedPath(null).ShouldBeFalse();
    }

    [Fact]
    public void AddEksenTelemetryCore_Should_Register_Options_And_Primitives_That_Validate()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEksenTelemetryCore(
            Section(new Dictionary<string, string?> { ["RedactedHeaders:0"] = "X-Custom" }),
            Environment("Production"),
            options => options.ServiceRole = "api");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var options = provider.GetRequiredService<IOptions<EksenTelemetryOptions>>().Value;
        options.ServiceRole.ShouldBe("api");
        provider.GetRequiredService<Eksen.Telemetry.Redaction.HeaderRedactor>().IsSensitive("x-custom").ShouldBeTrue();
        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetSourceNames(provider)
            .ShouldBe(["Eksen.Http.Outbound", "Eksen.Background"], ignoreOrder: true);
    }

    [Fact]
    public void Registering_A_Source_Twice_Should_Keep_One_Registration()
    {
        var services = new ServiceCollection();
        services.AddEksenTelemetrySource("Eksen.EventBus");
        services.AddEksenTelemetrySource("Eksen.EventBus");
        services.AddEksenTelemetryMeter("Eksen.EventBus");
        services.AddEksenTelemetrySource("Eksen.UnitOfWork");

        using var provider = services.BuildServiceProvider();

        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetSourceNames(provider).ShouldBe(["Eksen.EventBus", "Eksen.UnitOfWork"]);
        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetMeterNames(provider).ShouldBe(["Eksen.EventBus"]);
        services.Count(d => d.ServiceType == typeof(Eksen.Telemetry.Registry.EksenTelemetrySourceRegistration)).ShouldBe(2);
    }

    private static readonly Func<string, string?> NoVariables = _ => null;

    private static Func<string, string?> Variables(Dictionary<string, string> values)
    {
        return name => values.GetValueOrDefault(name);
    }

    private static IConfiguration Section(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => "Telemetry:" + pair.Key, pair => pair.Value))
            .Build()
            .GetSection("Telemetry");
    }

    private static IHostEnvironment Environment(string name)
    {
        return new TestHostEnvironment { EnvironmentName = name, ApplicationName = "TestApp" };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";

        public string ApplicationName { get; set; } = "TestApp";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
