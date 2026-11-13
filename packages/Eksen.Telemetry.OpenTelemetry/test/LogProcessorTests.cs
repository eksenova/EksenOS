using Eksen.Telemetry.OpenTelemetry.Logging;
using Eksen.TestBase;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Shouldly;

namespace Eksen.Telemetry.OpenTelemetry.Tests;

public class LogProcessorTests : EksenUnitTestBase
{
    [Fact]
    public void RedactingLogProcessor_Should_Redact_Attributes_Body_And_Message()
    {
        // Arrange
        var capture = new CapturingLogProcessor();
        using var factory = CreateFactory(capture);
        var logger = factory.CreateLogger("Tests");

        // Act
        logger.LogInformation(
            "Outbound {url.full} body {Body} for {password}",
            new Uri("https://bank.test/token?client_id=abc&client_secret=hidden"),
            "{\"access_token\":\"hidden\",\"expires_in\":3600}",
            "hunter2");

        // Assert
        var record = capture.Records.Single();
        record.Attributes["url.full"].ShouldBe("https://bank.test/token?client_id=abc&client_secret=[redacted]");
        record.Attributes["Body"].ShouldBe("{\"access_token\":\"[redacted]\",\"expires_in\":3600}");
        record.Attributes["password"].ShouldBe("[redacted]");
        record.Attributes["{OriginalFormat}"].ShouldBe("Outbound {url.full} body {Body} for {password}");
        record.FormattedMessage.ShouldNotBeNull();
        record.FormattedMessage.ShouldNotContain("hidden");
        record.FormattedMessage.ShouldContain("client_id=abc");
        record.FormattedMessage.ShouldContain("expires_in");
        record.FormattedMessage.ShouldNotContain("hunter2");
        record.FormattedMessage.ShouldEndWith("for [redacted]");
    }

    [Fact]
    public void TryRender_Should_Fill_Holes_With_Attribute_Values()
    {
        RedactingLogProcessor.TryRender(
            [
                new KeyValuePair<string, object?>("Amount", 12.5m),
                new KeyValuePair<string, object?>("token", "[redacted]"),
                new KeyValuePair<string, object?>("{OriginalFormat}", "Paid {Amount:F2} with {@token} {{literal}} {Missing}")
            ],
            out var rendered).ShouldBeTrue();

        rendered.ShouldBe("Paid 12.50 with [redacted] {literal} {Missing}");
        RedactingLogProcessor.TryRender([new KeyValuePair<string, object?>("a", 1)], out _).ShouldBeFalse();
    }

    [Fact]
    public void RedactingLogProcessor_Should_Leave_Other_Attributes_Untouched()
    {
        var capture = new CapturingLogProcessor();
        using var factory = CreateFactory(capture);

        factory.CreateLogger("Tests").LogInformation("Account {Iban} {Count}", "TR330006100519786457841326", 3);

        var record = capture.Records.Single();
        record.Attributes["Iban"].ShouldBe("TR330006100519786457841326");
        record.Attributes["Count"].ShouldBe(3);
        record.FormattedMessage.ShouldBe("Account TR330006100519786457841326 3");
    }

    [Fact]
    public void ExceptionNormalizingLogProcessor_Should_Move_The_Exception_Into_Attributes()
    {
        // Arrange
        var capture = new CapturingLogProcessor();
        using var factory = CreateFactory(capture);
        Exception exception;
        try
        {
            throw new InvalidOperationException("outer", new ArgumentException("inner"));
        }
        catch (Exception thrown)
        {
            exception = thrown;
        }

        // Act
        factory.CreateLogger("Tests").LogError(exception, "Failed for order {OrderId}", 42);

        // Assert
        var record = capture.Records.Single();
        record.Exception.ShouldBeNull();
        record.Attributes["exception.type"].ShouldBe("System.InvalidOperationException");
        record.Attributes["exception.message"].ShouldBe("outer");
        var stackTrace = record.Attributes["exception.stacktrace"].ShouldBeOfType<string>();
        stackTrace.ShouldNotContain("outer");
        stackTrace.ShouldContain(" ---> [System.ArgumentException]");
        record.Attributes["OrderId"].ShouldBe(42);
    }

    [Fact]
    public void ExceptionNormalizingLogProcessor_Should_Not_Touch_Records_Without_Exception()
    {
        var capture = new CapturingLogProcessor();
        using var factory = CreateFactory(capture);

        factory.CreateLogger("Tests").LogWarning("Plain {Value}", "x");

        capture.Records.Single().Attributes.Keys.ShouldBe(["Value", "{OriginalFormat}"], ignoreOrder: true);
    }

    [Fact]
    public void Scope_Values_Should_Be_Redacted_Before_Export()
    {
        // Arrange
        var capture = new CapturingLogProcessor();
        using var factory = CreateFactory(capture);
        var logger = factory.CreateLogger("Tests");

        // Act
        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["password"] = "hunter2",
                   ["url"] = "https://a.test/b?token=abc",
                   ["licenseKey"] = "0f8fad5b-d9cb-469f-a165-70867728950e",
                   ["eksen.user_id"] = "u-1"
               }))
        using (logger.BeginScope("Calling {Endpoint} with {client_secret}", "https://a.test/c", "s3cr3t"))
        {
            logger.LogError("Failed");
        }

        // Assert
        var scopes = capture.Records.Single().Scopes.ShouldNotBeNull();
        scopes["password"].ShouldBe("[redacted]");
        scopes["url"].ShouldBe("https://a.test/b?token=[redacted]");
        scopes["licenseKey"].ShouldBe("[redacted]");
        scopes["eksen.user_id"].ShouldBe("u-1");
        scopes["Endpoint"].ShouldBe("https://a.test/c");
        scopes["client_secret"].ShouldBe("[redacted]");
    }

    [Fact]
    public void Exception_Messages_Should_Be_Redacted()
    {
        var capture = new CapturingLogProcessor();
        using var factory = CreateFactory(capture);

        factory.CreateLogger("Tests").LogError(
            new HttpRequestException("GET https://api.test/items?access_token=SECRET failed"),
            "Call failed");

        var record = capture.Records.Single();
        record.Attributes["exception.message"].ShouldBe("GET https://api.test/items?access_token=[redacted] failed");
        foreach (var value in record.Attributes.Values)
        {
            (value?.ToString() ?? string.Empty).ShouldNotContain("SECRET");
        }
    }

    private static ILoggerFactory CreateFactory(CapturingLogProcessor capture)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.AddProcessor(new RedactingLogProcessor());
                options.AddProcessor(new ExceptionNormalizingLogProcessor());
                options.AddProcessor(capture);
            });
            ScopeRedactingLoggerProvider.Decorate(builder.Services);
        });
    }
}
