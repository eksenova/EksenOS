using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.Telemetry;
using Eksen.TestBase;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.ErrorHandling.AspNetCore.Tests;

public class ErrorHandlingTelemetryTests : EksenUnitTestBase
{
    private static readonly ActivitySource TestSource = new("Eksen.ErrorHandling.AspNetCore.Tests");

    private static IMeterFactory CreateMeterFactory()
    {
        return new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
    }

    [Fact]
    public async Task Client_Error_Records_The_Code_Without_Failing_The_Span()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.ErrorHandling, meterFactory);
        var logger = new Mock<ILogger<EksenExceptionHandler>>();
        var handler = CreateHandler(logger.Object, meterFactory);
        var descriptor = new ErrorDescriptor(ErrorType.Validation, "Orders", "InvalidQuantity");

        using var request = TestSource.StartActivity("request", ActivityKind.Server)!;
        (await handler.TryHandleAsync(CreateHttpContext(), new EksenException(descriptor), TestContext.Current.CancellationToken))
            .ShouldBeTrue();

        request.Status.ShouldBe(ActivityStatusCode.Unset);
        request.GetTagItem("error.type").ShouldBe("Orders.InvalidQuantity");
        request.GetTagItem("eksen.error.kind").ShouldBe(ErrorType.Validation);
        // A client error is an expected outcome: no exception event on the request span.
        request.Events.ShouldNotContain(activityEvent => activityEvent.Name == "exception");

        var handled = collector.MeasurementsOf("eksen.errors.handled").ShouldHaveSingleItem();
        handled.Tag("error.type").ShouldBe("Orders.InvalidQuantity");
        handled.Tag("eksen.error.kind").ShouldBe(ErrorType.Validation);
        handled.Tag("http.response.status_code").ShouldBe(400);
        logger.Verify(x => x.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Orders.InvalidQuantity")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Server_Error_Fails_The_Span_And_Logs_At_Error()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.ErrorHandling, meterFactory);
        var logger = new Mock<ILogger<EksenExceptionHandler>>();
        var handler = CreateHandler(logger.Object, meterFactory);
        var descriptor = new ErrorDescriptor("Infrastructure", "Payments", "ProviderUnavailable");
        var context = CreateHttpContext();

        using var request = TestSource.StartActivity("request", ActivityKind.Server)!;
        (await handler.TryHandleAsync(context, new InvalidOperationException("wrapped", new EksenException(descriptor)),
            TestContext.Current.CancellationToken)).ShouldBeTrue();

        context.Response.StatusCode.ShouldBe(500);
        request.Status.ShouldBe(ActivityStatusCode.Error);
        request.GetTagItem("error.type").ShouldBe("Payments.ProviderUnavailable");
        collector.MeasurementsOf("eksen.errors.handled").ShouldHaveSingleItem().Tag("http.response.status_code").ShouldBe(500);
        logger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Payments.ProviderUnavailable")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Server_Error_Event_Has_Its_Message_And_Stack_Trace_Redacted()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.ErrorHandling, meterFactory);
        var handler = CreateHandler(new Mock<ILogger<EksenExceptionHandler>>().Object, meterFactory);
        var descriptor = new ErrorDescriptor("Infrastructure", "Payments", "ProviderUnavailable");
        var exception = new InvalidOperationException(
            "GET https://pay.example.test/charge?token=secret-value failed",
            new EksenException(descriptor));

        using var request = TestSource.StartActivity("request", ActivityKind.Server)!;
        (await handler.TryHandleAsync(CreateHttpContext(), exception, TestContext.Current.CancellationToken)).ShouldBeTrue();

        var exceptionEvent = request.Events.ShouldHaveSingleItem();
        exceptionEvent.Name.ShouldBe("exception");
        var tags = exceptionEvent.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
        var message = tags["exception.message"].ShouldBeOfType<string>();
        var stackTrace = tags["exception.stacktrace"].ShouldBeOfType<string>();
        message.ShouldNotContain("secret-value");
        message.ShouldContain("token=");
        stackTrace.ShouldNotContain("secret-value");
        tags["exception.type"].ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task Server_Error_Adds_No_Event_When_Exceptions_Are_Not_Recorded()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.ErrorHandling, meterFactory);
        var formatter = new Mock<IErrorFormatter>();
        formatter.Setup(f => f.FormatError(It.IsAny<IErrorData>())).Returns("formatted");
        var options = new Mock<IOptionsMonitor<EksenTelemetryOptions>>();
        options.Setup(monitor => monitor.CurrentValue).Returns(new EksenTelemetryOptions { RecordExceptions = false });
        var handler = new EksenExceptionHandler(
            new Mock<ILogger<EksenExceptionHandler>>().Object, formatter.Object, meterFactory, options.Object);

        using var request = TestSource.StartActivity("request", ActivityKind.Server)!;
        (await handler.TryHandleAsync(CreateHttpContext(),
            new EksenException(new ErrorDescriptor("Infrastructure", "Payments", "ProviderUnavailable")),
            TestContext.Current.CancellationToken)).ShouldBeTrue();

        request.Events.ShouldBeEmpty();
        request.Status.ShouldBe(ActivityStatusCode.Error);
        request.GetTagItem("error.type").ShouldBe("Payments.ProviderUnavailable");
    }

    [Fact]
    public async Task Unhandled_Exception_Records_Nothing()
    {
        var meterFactory = CreateMeterFactory();
        using var collector = new TelemetryCollector(EksenTelemetry.ErrorHandling, meterFactory);
        var handler = CreateHandler(new Mock<ILogger<EksenExceptionHandler>>().Object, meterFactory);

        using var request = TestSource.StartActivity("request", ActivityKind.Server)!;
        (await handler.TryHandleAsync(CreateHttpContext(), new InvalidOperationException("boom"), TestContext.Current.CancellationToken))
            .ShouldBeFalse();

        request.GetTagItem("error.type").ShouldBeNull();
        collector.Measurements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Response_Body_Shape_Is_Unchanged()
    {
        var handler = CreateHandler(new Mock<ILogger<EksenExceptionHandler>>().Object, CreateMeterFactory());
        var context = CreateHttpContext();

        await handler.TryHandleAsync(context, new EksenException(new ErrorDescriptor(ErrorType.NotFound, "Orders", "Missing")),
            TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("{\"errorMessage\":\"formatted\"}");
    }

    [Fact]
    public void AddAspNetCoreSupport_Registers_The_Meter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEksen(eksen => eksen.AddLocalization().AddErrorHandling(errors => errors.AddAspNetCoreSupport()));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        Eksen.Telemetry.Registry.EksenTelemetryRegistry.GetMeterNames(provider).ShouldContain(EksenTelemetry.ErrorHandling);
    }

    private static EksenExceptionHandler CreateHandler(ILogger<EksenExceptionHandler> logger, IMeterFactory meterFactory)
    {
        var formatter = new Mock<IErrorFormatter>();
        formatter.Setup(f => f.FormatError(It.IsAny<IErrorData>())).Returns("formatted");
        return new EksenExceptionHandler(logger, formatter.Object, meterFactory);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.TraceIdentifier = "test-trace-id";
        return httpContext;
    }
}
