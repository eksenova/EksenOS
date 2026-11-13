using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Eksen.Telemetry.AspNetCore.Enrichment;
using Eksen.Telemetry.AspNetCore.Middlewares;
using Eksen.TestBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Eksen.Telemetry.AspNetCore.Tests;

public class RequestTelemetryMiddlewareTests : EksenUnitTestBase
{
    [Fact]
    public async Task TraceIdentifier_Should_Equal_The_Activity_Trace_Id()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(endpoints =>
            endpoints.MapGet("/trace", (HttpContext context) => new
            {
                context.TraceIdentifier,
                TraceId = Activity.Current!.TraceId.ToHexString(),
                SpanId = Activity.Current!.SpanId.ToHexString()
            }));

        // Act
        using var response = await host.Client.GetAsync("/trace", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<TraceBody>(TestContext.Current.CancellationToken);

        // Assert
        body.ShouldNotBeNull();
        body.TraceIdentifier.ShouldBe(body.TraceId);
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe(body.TraceId);
        response.Headers.GetValues("X-Request-Id").Single().ShouldBe(body.SpanId);
        response.Headers.Contains("X-Powered-By").ShouldBeFalse();
    }

    [Fact]
    public async Task The_Completion_Event_Should_Carry_The_Request_Fields()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapGet("/api/products/{productId}", (string productId) => Results.Ok(productId)),
            configureServices: services =>
            {
                services.AddEksenRequestEnrichmentContributor<UserIdContributor>();
                services.AddEksenUrlPathScrubRule("^/api/products/secret-[^/]+", "/api/products/{secret}");
            });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products/secret-42?page=2&token=abc");
        request.Headers.Add("Authorization", "Bot abc.def.ghi");
        request.Headers.Add("Cookie", "sid=xyz");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        // Act
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completion = host.CompletionEvent();
        completion.Level.ShouldBe(LogLevel.Information);
        completion.Message.ShouldStartWith("HTTP GET /api/products/{secret} ended with 200 in ");
        completion.Properties["{OriginalFormat}"].ShouldBe(RequestCompletionLoggingMiddleware.MessageTemplate);
        completion.Properties["http.request.method"].ShouldBe("GET");
        completion.Properties["url.path"].ShouldBe("/api/products/{secret}");
        completion.Properties["url.query"].ShouldBe("?page=2&token=[redacted]");
        completion.Properties["url.full"].ShouldBe("http://localhost/api/products/{secret}?page=2&token=[redacted]");
        completion.Properties["http.response.status_code"].ShouldBe(200);
        completion.Properties["http.route"].ShouldBe("/api/products/{productId}");
        completion.Properties["eksen.http.query.page"].ShouldBe("2");
        completion.Properties["eksen.http.query.token"].ShouldBe("[redacted]");
        completion.Properties["http.request.header.authorization"].ShouldBe("***");
        completion.Properties["http.request.header.cookie"].ShouldBe("***");
        completion.Properties["client.address"].ShouldBe("203.0.113.7");
        completion.Properties["eksen.user_id"].ShouldBe("user-1");
        completion.Properties.ShouldContainKey("http.server.request.duration_ms");
        completion.Properties.ShouldContainKey("eksen.trace_id");
        completion.Properties["eksen.http.request_id"].ShouldBe(completion.Properties["eksen.trace_id"]);
    }

    [Fact]
    public async Task Redacted_Headers_Should_Not_Appear_In_Any_Log_Event()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapGet("/log", (ILogger<RequestTelemetryMiddlewareTests> logger) =>
            {
                logger.LogInformation("Inside the handler");
                return Results.Ok();
            }),
            settings: new Dictionary<string, string?> { ["RedactedHeaders:0"] = "X-Signature-Ed25519" });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/log");
        request.Headers.Add("Authorization", "Bearer very-secret-token");
        request.Headers.Add("X-Api-Key", "very-secret-key");
        request.Headers.Add("X-Signature-Ed25519", "very-secret-signature");

        // Act
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var handlerEvent = host.Logs.Events.Single(e => e.Message == "Inside the handler");
        handlerEvent.Properties["http.request.header.authorization"].ShouldBe("***");
        handlerEvent.Properties["http.request.method"].ShouldBe("GET");

        foreach (var logEvent in host.Logs.Events)
        {
            logEvent.Message.ShouldNotContain("very-secret");
            foreach (var value in logEvent.Properties.Values)
            {
                (value?.ToString() ?? string.Empty).ShouldNotContain("very-secret");
            }
        }
    }

    [Fact]
    public async Task Licence_Keys_And_Secret_Named_Headers_Should_Not_Appear_In_Any_Log_Event()
    {
        // Arrange
        const string licenceKey = "0f8fad5b-d9cb-469f-a165-70867728950e";
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapGet("/api/registrations", (ILogger<RequestTelemetryMiddlewareTests> logger) =>
            {
                logger.LogInformation("Listing registrations");
                return Results.Ok();
            }));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/registrations?licenseKey={licenceKey}&take=20");
        request.Headers.Add("X-Discord-Bot-Key", "botsecret123");

        // Act
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var completion = host.CompletionEvent();
        completion.Properties["eksen.http.query.licenseKey"].ShouldBe("[redacted]");
        completion.Properties["url.query"].ShouldBe("?licenseKey=[redacted]&take=20");
        completion.Properties["http.request.header.x-discord-bot-key"].ShouldBe("***");

        // The framework's own "Request starting" event logs the raw URL; the export pipeline (the redacting log
        // processor and console formatter) covers it, so only the events written by this package and the
        // handler, which carries the request scope, are checked here.
        var ownEvents = host.Logs.Events.Where(e =>
            e.Category == EksenTelemetry.RequestLogCategory || e.Message == "Listing registrations").ToArray();
        ownEvents.Length.ShouldBe(2);
        foreach (var logEvent in ownEvents)
        {
            logEvent.Message.ShouldNotContain(licenceKey);
            foreach (var value in logEvent.Properties.Values)
            {
                var text = value?.ToString() ?? string.Empty;
                text.ShouldNotContain(licenceKey);
                text.ShouldNotContain("botsecret123");
            }
        }
    }

    [Fact]
    public async Task An_Oversized_Adversarial_Header_Should_Be_Bounded_And_Cheap_To_Log()
    {
        // Arrange: a 30 KB header (under Kestrel's 32 KB limit) built to make backtracking redaction quadratic
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapGet("/log", (ILogger<RequestTelemetryMiddlewareTests> logger) =>
            {
                for (var line = 0; line < 5; line++)
                {
                    logger.LogInformation("Inside the handler {Line}", line);
                }

                return Results.Ok();
            }));

        var junk = "<" + string.Concat(Enumerable.Repeat("token", 30 * 1024 / 5));
        using var warmUp = await host.Client.GetAsync("/log", TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/log");
        request.Headers.TryAddWithoutValidation("X-Junk", junk);
        request.Headers.TryAddWithoutValidation("User-Agent", junk);

        // Act
        var stopwatch = Stopwatch.StartNew();
        using var response = await host.Client.SendAsync(request, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));

        var expected = junk[..HttpRequestEnrichmentCollector.MaxValueLength] + "...[truncated]";
        var handlerEvent = host.Logs.Events.Last(e => e.Message == "Inside the handler 4");
        handlerEvent.Properties["http.request.header.x-junk"].ShouldBe(expected);
        handlerEvent.Properties["user_agent.original"].ShouldBe(expected);
        host.Logs.Events
            .Last(e => e.Category == EksenTelemetry.RequestLogCategory && e.Message.StartsWith("HTTP ", StringComparison.Ordinal))
            .Properties["http.request.header.x-junk"].ShouldBe(expected);
    }

    [Fact]
    public async Task The_Captured_Body_Should_Be_On_The_Completion_Event_Only()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapPost("/api/orders", async (HttpRequest request, ILogger<RequestTelemetryMiddlewareTests> logger) =>
            {
                logger.LogInformation("Placing the order");
                return await new StreamReader(request.Body).ReadToEndAsync();
            }),
            settings: new Dictionary<string, string?> { ["CaptureRequestBodies"] = "true" });

        // Act
        using var response = await host.Client.PostAsync(
            "/api/orders",
            new StringContent("""{"sku":"kit-1"}""", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        // Assert
        host.Logs.Events.Single(e => e.Message == "Placing the order").Properties.ShouldNotContainKey("eksen.request_body");
        host.CompletionEvent().Properties["eksen.request_body"].ShouldBe("""{"sku":"kit-1"}""");
    }

    [Fact]
    public async Task Server_Errors_Should_Be_Logged_As_Error_With_The_Exception()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(endpoints =>
            endpoints.MapGet("/boom", (Func<string>)(() => throw new InvalidOperationException("boom"))));

        // Act: TestServer surfaces the unhandled exception to the caller
        await Should.ThrowAsync<InvalidOperationException>(() => host.Client.GetAsync("/boom", TestContext.Current.CancellationToken));

        // Assert
        var completion = host.CompletionEvent();
        completion.Level.ShouldBe(LogLevel.Error);
        completion.Exception.ShouldBeOfType<InvalidOperationException>();
        completion.Properties["http.response.status_code"].ShouldBe(500);
    }

    [Fact]
    public async Task Excluded_Paths_Should_Not_Produce_A_Completion_Event()
    {
        await using var host = await TelemetryTestHost.StartAsync(endpoints => endpoints.MapGet("/scalar/v1", () => "docs"));

        using var response = await host.Client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        host.Logs.Events.ShouldNotContain(e => e.Category == EksenTelemetry.RequestLogCategory);
    }

    [Fact]
    public async Task Request_Bodies_Should_Be_Captured_Redacted_Only_When_Enabled()
    {
        // Arrange
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints =>
            {
                endpoints.MapPost("/api/login", async (HttpRequest request) => await new StreamReader(request.Body).ReadToEndAsync());
                endpoints.MapPost("/api/payments/callback", async (HttpRequest request) => await new StreamReader(request.Body).ReadToEndAsync());
            },
            settings: new Dictionary<string, string?> { ["CaptureRequestBodies"] = "true" },
            configureServices: services => services.AddEksenRequestBodyCaptureExclusions("/api/payments"));

        const string body = """{"user":"alice","password":"hidden"}""";

        // Act
        using var captured = await host.Client.PostAsync("/api/login", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        using var excluded = await host.Client.PostAsync("/api/payments/callback", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        // Assert: the endpoint still reads the full body
        (await captured.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("hidden");

        var events = host.Logs.Events.Where(e => e.Category == EksenTelemetry.RequestLogCategory).ToArray();
        events.Single(e => Equals(e.Properties["url.path"], "/api/login")).Properties["eksen.request_body"]
            .ShouldBe("""{"user":"alice","password":"[redacted]"}""");
        events.Single(e => Equals(e.Properties["url.path"], "/api/payments/callback")).Properties
            .ShouldNotContainKey("eksen.request_body");
    }

    [Fact]
    public async Task Request_Bodies_Should_Not_Be_Captured_For_Paths_Excluded_By_A_Predicate()
    {
        // Arrange: a route whose first segment is a secret cannot be excluded by a prefix
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints =>
            {
                endpoints.MapPost("/{key}/LicenseChecker/check", async (HttpRequest request) => await new StreamReader(request.Body).ReadToEndAsync());
                endpoints.MapPost("/api/orders", async (HttpRequest request) => await new StreamReader(request.Body).ReadToEndAsync());
            },
            settings: new Dictionary<string, string?> { ["CaptureRequestBodies"] = "true" },
            configureServices: services => services.AddEksenRequestBodyCaptureExclusion(path => path.Contains("/LicenseChecker/", StringComparison.OrdinalIgnoreCase)));

        const string body = """{"server":"s1"}""";

        // Act
        using var excluded = await host.Client.PostAsync("/0f8fad5b-d9cb-469f-a165-70867728950e/LicenseChecker/check", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);
        using var captured = await host.Client.PostAsync("/api/orders", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        // Assert: the endpoint still reads the body, the completion event carries it only for the other route
        (await excluded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(body);
        var events = host.Logs.Events.Where(e => e.Category == EksenTelemetry.RequestLogCategory).ToArray();
        events.Single(e => Equals(e.Properties["url.path"], "/api/orders")).Properties["eksen.request_body"].ShouldBe(body);
        events.Single(e => !Equals(e.Properties["url.path"], "/api/orders")).Properties.ShouldNotContainKey("eksen.request_body");
    }

    [Fact]
    public async Task Request_Bodies_Should_Not_Be_Captured_By_Default()
    {
        await using var host = await TelemetryTestHost.StartAsync(endpoints =>
            endpoints.MapPost("/api/login", async (HttpRequest request) => await new StreamReader(request.Body).ReadToEndAsync()));

        using var response = await host.Client.PostAsync("/api/login", new StringContent("{\"a\":1}", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        host.CompletionEvent().Properties.ShouldNotContainKey("eksen.request_body");
    }

    [Fact]
    public async Task Contributors_Should_Tag_The_Current_Activity_And_The_Handler_Logs()
    {
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapGet("/me", (ILogger<RequestTelemetryMiddlewareTests> logger) =>
            {
                logger.LogInformation("handler");
                return Activity.Current!.GetTagItem("eksen.user_id")?.ToString();
            }),
            configureServices: services => services.AddEksenRequestEnrichmentContributor<UserIdContributor>());

        var tag = await host.Client.GetStringAsync("/me", TestContext.Current.CancellationToken);

        tag.ShouldBe("user-1");
        host.Logs.Events.Single(e => e.Message == "handler").Properties["eksen.user_id"].ShouldBe("user-1");
        host.Logs.Events.Single(e => e.Message == "handler").Properties.ShouldNotContainKey("eksen.internal_note");
    }

    [Fact]
    public async Task Guarded_Paths_Should_Be_Hidden_From_Non_Loopback_Callers()
    {
        await using var host = await TelemetryTestHost.StartAsync(
            endpoints => endpoints.MapGet("/internal/stats", () => "ok"),
            configureServices: services => services.ConfigureEksenInternalEndpointGuard(options => options.GuardedPathPrefixes.Add("/internal")));

        // TestServer requests have no remote address, which is treated as external.
        using var response = await host.Client.GetAsync("/internal/stats", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("10.244.1.5", false)]
    [InlineData("203.0.113.7", false)]
    public void Only_Loopback_Is_Allowed_By_Default(string address, bool allowed)
    {
        InternalEndpointGuardMiddleware.IsAllowed(IPAddress.Parse(address), new InternalEndpointGuardOptions()).ShouldBe(allowed);
    }

    [Fact]
    public void Configured_Networks_Should_Be_Allowed()
    {
        var options = new InternalEndpointGuardOptions();
        options.AllowedNetworks.Add("10.244.0.0/16");

        InternalEndpointGuardMiddleware.IsAllowed(IPAddress.Parse("10.244.1.5"), options).ShouldBeTrue();
        InternalEndpointGuardMiddleware.IsAllowed(IPAddress.Parse("10.245.1.5"), options).ShouldBeFalse();
    }

    private sealed record TraceBody(string TraceIdentifier, string TraceId, string SpanId);

    private sealed class UserIdContributor : IRequestEnrichmentContributor
    {
        public IEnumerable<RequestEnrichmentProperty> Collect(HttpContext context)
        {
            yield return new RequestEnrichmentProperty("eksen.user_id", "user-1");
            yield return new RequestEnrichmentProperty("eksen.internal_note", null);
        }
    }
}
