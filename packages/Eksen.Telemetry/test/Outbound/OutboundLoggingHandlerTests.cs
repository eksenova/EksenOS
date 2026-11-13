using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Eksen.Telemetry.Outbound;
using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.Telemetry.Tests.Outbound;

public class OutboundLoggingHandlerTests : EksenUnitTestBase
{
    private const string Url = "https://partner.invalid/v1/usage/application";

    [Fact]
    public async Task Chunked_Json_Response_Should_Be_Captured_And_Still_Readable_By_The_Caller()
    {
        const string body = """{"errorCode":"SP-1042","message":"Application not found for api key"}""";
        using var fixture = new Fixture(ChunkedJson(HttpStatusCode.BadRequest, body));

        using var response = await fixture.SendGetAsync();

        fixture.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBe(body);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(body);
    }

    [Fact]
    public async Task Chunked_Xml_Response_Should_Be_Captured_And_Still_Readable_By_The_Caller()
    {
        const string body = "<Invoice><ID>INV2026000000001</ID></Invoice>";
        using var fixture = new Fixture(Chunked(HttpStatusCode.OK, body, "application/xml"));

        using var response = await fixture.SendGetAsync();

        fixture.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBe(body);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(body);
    }

    [Fact]
    public async Task A_Chunked_Response_Beyond_The_Capture_Limit_Should_Still_Reach_The_Caller_Intact()
    {
        var body = new string('x', 400 * 1024);
        using var fixture = new Fixture(ChunkedJson(HttpStatusCode.OK, body));

        using var response = await fixture.SendGetAsync();

        var captured = fixture.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty) as string;
        captured.ShouldNotBeNull();
        captured.ShouldEndWith("...[truncated]");
        captured.Length.ShouldBe(OutboundLoggingHandler.MaxLoggedBodyChars + "...[truncated]".Length);

        var received = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        received.ShouldBe(body);
    }

    [Fact]
    public async Task A_Buffered_Chunked_Response_Should_Stay_Readable_More_Than_Once()
    {
        const string body = """{"errorCode":"SP-1042"}""";
        using var fixture = new Fixture(ChunkedJson(HttpStatusCode.BadRequest, body));

        using var response = await fixture.SendGetAsync();

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(body);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(body);
    }

    [Fact]
    public async Task An_Empty_Chunked_Response_Should_Be_Reported_As_Empty()
    {
        using var fixture = new Fixture(ChunkedJson(HttpStatusCode.NoContent, ""));

        using var response = await fixture.SendGetAsync();

        fixture.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBe("[empty]");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("");
    }

    [Fact]
    public async Task An_Event_Stream_Response_Should_Not_Be_Buffered()
    {
        using var fixture = new Fixture(Chunked(HttpStatusCode.OK, "data: first\n\n", "text/event-stream"));

        using var response = await fixture.SendGetAsync();

        fixture.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBe("[stream not captured]");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("data: first\n\n");
    }

    [Fact]
    public async Task A_Chunked_Binary_Response_Should_Not_Be_Buffered()
    {
        using var fixture = new Fixture(Chunked(HttpStatusCode.OK, "%PDF-1.7", "application/pdf"));

        using var response = await fixture.SendGetAsync();

        fixture.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBe("[binary content]");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("%PDF-1.7");
    }

    [Fact]
    public async Task A_Request_Body_Should_Be_Captured_Redacted_And_Delivered_Unchanged()
    {
        const string body = """{"applicationId":282350,"client_secret":"hidden"}""";
        HttpContent? delivered = null;
        using var fixture = new Fixture(request =>
        {
            delivered = request.Content;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(body)))
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            }
        };

        using var response = await fixture.Invoker.SendAsync(request, TestContext.Current.CancellationToken);

        fixture.Logs.Property(OutboundLoggingHandler.RequestBodyProperty)
            .ShouldBe("""{"applicationId":282350,"client_secret":"[redacted]"}""");
        delivered.ShouldNotBeNull();
        (await delivered.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(body);
    }

    [Fact]
    public async Task The_Application_Body_Redactor_Should_Run_After_The_Generic_One()
    {
        using var fixture = new Fixture(
            ChunkedJson(HttpStatusCode.OK, """{"code":"123456","password":"x"}"""),
            bodyRedactor: new CodeRedactor());

        using var response = await fixture.SendGetAsync();

        fixture.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty)
            .ShouldBe("""{"code":"******","password":"[redacted]"}""");
    }

    [Fact]
    public async Task Bodies_Should_Not_Be_Captured_Unless_Enabled_And_Opted_In()
    {
        const string body = """{"ok":true}""";

        using var disabled = new Fixture(ChunkedJson(HttpStatusCode.OK, body), captureEnabled: false);
        using (await disabled.SendGetAsync())
        {
            disabled.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBeNull();
        }

        using var notOptedIn = new Fixture(ChunkedJson(HttpStatusCode.OK, body), optInClient: false);
        using (await notOptedIn.SendGetAsync())
        {
            notOptedIn.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBeNull();
        }

        using var perRequest = new Fixture(ChunkedJson(HttpStatusCode.OK, body), optInClient: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, Url).WithOutboundBodyCapture();
        using (await perRequest.Invoker.SendAsync(request, TestContext.Current.CancellationToken))
        {
            perRequest.Logs.Property(OutboundLoggingHandler.ResponseBodyProperty).ShouldBe(body);
        }
    }

    [Fact]
    public async Task The_Log_Event_Should_Carry_Redacted_Headers_And_A_Scrubbed_Url()
    {
        using var fixture = new Fixture(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") };
            response.Headers.Add("Set-Cookie", "sid=abc");
            return response;
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.invalid/api/webhooks/123/secret-token?wait=true&token=abc");
        request.Headers.TryAddWithoutValidation("Authorization", "Bot abc.def.ghi");
        request.Headers.Add("X-Trace", "visible");

        using var response = await fixture.Invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var logEvent = fixture.Logs.Events.Single();
        logEvent.Level.ShouldBe(LogLevel.Warning);
        logEvent.Properties["http.request.header.authorization"].ShouldBe("***");
        logEvent.Properties["http.request.header.x-trace"].ShouldBe("visible");
        logEvent.Properties["http.response.header.set-cookie"].ShouldBe("***");
        logEvent.Properties["url.full"].ShouldBe("https://discord.invalid/api/webhooks/123/{token}?wait=true&token=[redacted]");
        logEvent.Properties["http.response.status_code"].ShouldBe(401);
        logEvent.Properties["eksen.outbound.target"].ShouldBe("partner");
        logEvent.Properties.ShouldContainKey("eksen.http.client.duration_ms");
        logEvent.Message.ShouldNotContain("secret-token");
        logEvent.Message.ShouldNotContain("abc.def.ghi");
    }

    [Fact]
    public async Task Successful_Calls_Log_Information_And_Record_Metrics()
    {
        using var fixture = new Fixture(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[12])
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new ByteArrayContent(new byte[5])
        };
        request.WithOutboundTarget("billing");

        using var response = await fixture.Invoker.SendAsync(request, TestContext.Current.CancellationToken);

        fixture.Logs.Events.Single().Level.ShouldBe(LogLevel.Information);

        var requests = fixture.Measurements("eksen.outbound.http.requests").Single();
        requests.Value.ShouldBe(1);
        requests.Tags["server.address"].ShouldBe("partner.invalid");
        requests.Tags["http.request.method"].ShouldBe("POST");
        requests.Tags["http.response.status_code"].ShouldBe(200);
        requests.Tags["eksen.outbound.target"].ShouldBe("billing");

        fixture.Measurements("eksen.outbound.http.request.duration").Single().Tags.ShouldNotContainKey("http.response.status_code");
        fixture.Measurements("eksen.outbound.http.bytes.sent").Single().Value.ShouldBe(5);
        fixture.Measurements("eksen.outbound.http.bytes.received").Single().Value.ShouldBe(12);
        fixture.Measurements("eksen.outbound.http.in_flight").Select(m => m.Value).ShouldBe([1d, -1d]);
    }

    [Fact]
    public async Task Transport_Failures_Log_Error_And_Record_The_Error_Type()
    {
        using var fixture = new Fixture(_ => throw new HttpRequestException("refused"));

        await Should.ThrowAsync<HttpRequestException>(() => fixture.SendGetAsync());

        var logEvent = fixture.Logs.Events.Single();
        logEvent.Level.ShouldBe(LogLevel.Error);
        logEvent.Exception.ShouldBeOfType<HttpRequestException>();

        var requests = fixture.Measurements("eksen.outbound.http.requests").Single();
        requests.Tags["error.type"].ShouldBe("connection_error");
        requests.Tags.ShouldNotContainKey("http.response.status_code");
        fixture.Measurements("eksen.outbound.http.in_flight").Sum(m => m.Value).ShouldBe(0);
    }

    [Theory]
    [InlineData(true, "canceled")]
    [InlineData(false, "timeout")]
    public void Cancellation_Is_Classified_By_The_Caller_Token(bool callerCanceled, string expected)
    {
        using var source = new CancellationTokenSource();
        if (callerCanceled)
        {
            source.Cancel();
        }

        OutboundLoggingHandler.ClassifyError(new TaskCanceledException(), source.Token).ShouldBe(expected);
        OutboundLoggingHandler.ClassifyError(new InvalidDataException(), source.Token).ShouldBe("InvalidDataException");
    }

    [Fact]
    public void Timeouts_Are_Recognised_From_The_Exception_And_The_Client_Timeout()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        OutboundLoggingHandler.ClassifyError(new TaskCanceledException("connect", new TimeoutException()), canceled.Token)
            .ShouldBe("timeout");
        OutboundLoggingHandler.ClassifyError(new TimeoutRejectedException(), CancellationToken.None).ShouldBe("timeout");
        OutboundLoggingHandler.ClassifyError(new TaskCanceledException(), canceled.Token, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10))
            .ShouldBe("timeout");
        OutboundLoggingHandler.ClassifyError(new TaskCanceledException(), canceled.Token, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10))
            .ShouldBe("canceled");
        OutboundLoggingHandler.ClassifyError(new TaskCanceledException(), canceled.Token, TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan)
            .ShouldBe("canceled");
    }

    [Fact]
    public async Task An_Expired_Client_Timeout_Should_Be_Recorded_As_Timeout()
    {
        // Arrange
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddEksenTelemetryPrimitives();
        services.AddEksenOutboundTelemetry();
        services.AddHttpClient("slow-partner", client => client.Timeout = TimeSpan.FromMilliseconds(300))
            .ConfigurePrimaryHttpMessageHandler(() => new DelayingHandler(TimeSpan.FromSeconds(3)));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("slow-partner");

        // Act
        await Should.ThrowAsync<TaskCanceledException>(() =>
            client.GetAsync("https://partner.invalid/slow", TestContext.Current.CancellationToken));

        // Assert
        logs.Events.Single(e => e.Category == typeof(OutboundLoggingHandler).FullName)
            .Properties["error.type"].ShouldBe("timeout");
    }

    [Fact]
    public async Task A_Caller_Cancellation_Should_Be_Recorded_As_Canceled()
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddEksenTelemetryPrimitives();
        services.AddEksenOutboundTelemetry();
        services.AddHttpClient("slow-partner", client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new DelayingHandler(TimeSpan.FromSeconds(3)));

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("slow-partner");
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        caller.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Should.ThrowAsync<TaskCanceledException>(() => client.GetAsync("https://partner.invalid/slow", caller.Token));

        logs.Events.Single(e => e.Category == typeof(OutboundLoggingHandler).FullName)
            .Properties["error.type"].ShouldBe("canceled");
    }

    [Fact]
    public async Task Secret_Named_Headers_Should_Be_Masked_In_The_Log_Scope()
    {
        using var fixture = new Fixture(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var request = new HttpRequestMessage(HttpMethod.Get, Url + "?access_token=abc");
        request.Headers.Add("X-Discord-Bot-Key", "botsecret123");

        using var response = await fixture.Invoker.SendAsync(request, TestContext.Current.CancellationToken);

        var logEvent = fixture.Logs.Events.Single();
        logEvent.Properties["http.request.header.x-discord-bot-key"].ShouldBe("***");
        foreach (var value in logEvent.Properties.Values)
        {
            (value?.ToString() ?? string.Empty).ShouldNotContain("botsecret123");
            (value?.ToString() ?? string.Empty).ShouldNotContain("access_token=abc");
        }
    }

    [Fact]
    public async Task Http_Client_Factory_Clients_Should_Get_The_Handler_Named_After_The_Client()
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddEksenTelemetryPrimitives();
        services.AddEksenOutboundTelemetry();
        services.AddHttpClient("discord")
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        using var response = await provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("discord")
            .GetAsync("https://discord.invalid/api/gateway", TestContext.Current.CancellationToken);

        logs.Events.Single(e => e.Category == typeof(OutboundLoggingHandler).FullName)
            .Properties["eksen.outbound.target"].ShouldBe("discord");
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> ChunkedJson(HttpStatusCode statusCode, string body)
    {
        return Chunked(statusCode, body, "application/json");
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> Chunked(HttpStatusCode statusCode, string body, string mediaType)
    {
        return _ =>
        {
            var content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(body)));
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            content.Headers.ContentLength = null;

            return new HttpResponseMessage(statusCode) { Content = content };
        };
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly MeterListener _listener = new();
        private readonly List<(string Instrument, double Value, Dictionary<string, object?> Tags)> _measurements = [];

        public Fixture(
            Func<HttpRequestMessage, HttpResponseMessage> respond,
            bool captureEnabled = true,
            bool optInClient = true,
            IOutboundBodyRedactor? bodyRedactor = null)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(Logs).SetMinimumLevel(LogLevel.Trace));
            services.AddEksenTelemetryPrimitives();
            services.Configure<EksenTelemetryOptions>(options => options.CaptureOutboundBodies = captureEnabled);
            if (optInClient)
            {
                services.AddEksenOutboundBodyCapture("partner");
            }

            _provider = services.BuildServiceProvider();

            var metrics = _provider.GetRequiredService<OutboundHttpMetrics>();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == metrics.Meter)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
            _listener.Start();

            var handler = new OutboundLoggingHandler(
                _provider.GetRequiredService<ILogger<OutboundLoggingHandler>>(),
                _provider.GetRequiredService<HeaderRedactor>(),
                metrics,
                new UrlPathScrubberStub(),
                bodyRedactor ?? new NullOutboundBodyRedactor(),
                _provider.GetRequiredService<IOptionsMonitor<EksenTelemetryOptions>>(),
                _provider.GetRequiredService<IOptionsMonitor<OutboundBodyCaptureOptions>>(),
                "partner")
            {
                InnerHandler = new StubHandler(respond)
            };

            Invoker = new HttpMessageInvoker(handler);
        }

        public CapturingLoggerProvider Logs { get; } = new();

        public HttpMessageInvoker Invoker { get; }

        public Task<HttpResponseMessage> SendGetAsync()
        {
            return Invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url), TestContext.Current.CancellationToken);
        }

        public IReadOnlyList<(double Value, Dictionary<string, object?> Tags)> Measurements(string instrument)
        {
            lock (_measurements)
            {
                return _measurements.Where(m => m.Instrument == instrument).Select(m => (m.Value, m.Tags)).ToArray();
            }
        }

        public void Dispose()
        {
            _listener.Dispose();
            Invoker.Dispose();
            _provider.Dispose();
        }

        private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copy = new Dictionary<string, object?>();
            foreach (var (key, tagValue) in tags)
            {
                copy[key] = tagValue;
            }

            lock (_measurements)
            {
                _measurements.Add((instrument.Name, value, copy));
            }
        }
    }

    private sealed class DelayingHandler(TimeSpan delay) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    // Stands in for Polly's exception of the same name, which is matched by type name.
    private sealed class TimeoutRejectedException() : Exception("The delegate executed asynchronously through TimeoutPolicy did not complete within the timeout.");

    private sealed class UrlPathScrubberStub : IUrlPathScrubber
    {
        public string Scrub(string path)
        {
            return System.Text.RegularExpressions.Regex.Replace(path, @"/webhooks/(\d+)/[^/]+", "/webhooks/$1/{token}");
        }
    }

    private sealed class CodeRedactor : IOutboundBodyRedactor
    {
        public string Redact(HttpRequestMessage request, string body, bool isResponse)
        {
            return System.Text.RegularExpressions.Regex.Replace(body, "(\"code\":\")[^\"]*", "$1******");
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = respond(request);
            response.RequestMessage = request;

            return Task.FromResult(response);
        }
    }
}
