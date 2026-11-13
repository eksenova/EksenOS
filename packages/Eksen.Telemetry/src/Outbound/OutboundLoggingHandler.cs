using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Eksen.Telemetry.Redaction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.Outbound;

/// <summary>
/// Logs and measures every outbound HTTP call: one log event per call (Information on 2xx, Warning on other
/// status codes, Error on transport failures), the <c>Eksen.Http.Outbound</c> metrics and, when enabled for the
/// client, the redacted request and response bodies.
/// </summary>
public sealed class OutboundLoggingHandler(
    ILogger<OutboundLoggingHandler> logger,
    HeaderRedactor headerRedactor,
    OutboundHttpMetrics metrics,
    IUrlPathScrubber urlPathScrubber,
    IOutboundBodyRedactor bodyRedactor,
    IOptionsMonitor<EksenTelemetryOptions> telemetryOptions,
    IOptionsMonitor<OutboundBodyCaptureOptions> bodyCaptureOptions,
    string? clientName = null,
    TimeSpan? clientTimeout = null
) : DelegatingHandler
{
    public const int MaxLoggedBodyChars = 4096;
    public const long MaxReadableBodyBytes = 256 * 1024;
    public const string TruncationMarker = "...[truncated]";
    public const string RequestBodyProperty = "eksen.http.request_body";
    public const string ResponseBodyProperty = "eksen.http.response_body";
    public const string DurationProperty = "eksen.http.client.duration_ms";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Measured from the handler's entry, so a client timeout that expires while the request body is being
        // buffered is still recognised as a timeout.
        var handlerStartTimestamp = Stopwatch.GetTimestamp();
        var target = ResolveTarget(request);
        if (!request.Options.TryGetValue(OutboundRequestOptions.Target, out _))
        {
            // Lets the HttpClient span enrichment see the target resolved from the client name.
            request.Options.Set(OutboundRequestOptions.Target, target);
        }

        Activity.Current?.SetTag(EksenSemanticConventions.EksenOutboundTarget, target);

        var method = request.Method.Method;
        var requestUri = request.RequestUri;
        var serverAddress = ResolveServerAddress(requestUri);
        var loggedUrl = requestUri is null ? null : urlPathScrubber.ScrubUrl(requestUri);
        var captureBodies = ShouldCaptureBodies(request);

        var requestBodySize = request.Content?.Headers.ContentLength;
        string? requestBody = null;
        if (captureBodies)
        {
            // The request body is buffered before the inner handler reads it, so the captured copy and the
            // bytes actually sent stay identical.
            var requestCapture = await CaptureBodyAsync(request.Content, cancellationToken);
            if (requestCapture.Replacement is not null)
            {
                request.Content = requestCapture.Replacement;
            }

            requestBody = RedactBody(request, requestCapture, request.Content?.Headers.ContentType?.MediaType, isResponse: false);
        }

        var transferTags = new TagList
        {
            { EksenSemanticConventions.ServerAddress, serverAddress },
            { EksenSemanticConventions.HttpRequestMethod, method },
            { EksenSemanticConventions.EksenOutboundTarget, target }
        };

        var inFlightTags = new TagList
        {
            { EksenSemanticConventions.ServerAddress, serverAddress },
            { EksenSemanticConventions.EksenOutboundTarget, target }
        };

        metrics.InFlight.Add(1, inFlightTags);

        var scope = new Dictionary<string, object?>(StringComparer.Ordinal);
        AddRequestProperties(scope, request, target, method, requestUri, loggedUrl, requestBodySize, requestBody);

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            var statusCode = (int)response.StatusCode;

            var completedTags = transferTags;
            completedTags.Add(EksenSemanticConventions.HttpResponseStatusCode, statusCode);

            metrics.Requests.Add(1, completedTags);
            metrics.RequestDuration.Record(elapsed.TotalSeconds, transferTags);

            if (requestBodySize > 0)
            {
                metrics.BytesSent.Add(requestBodySize.Value, transferTags);
            }

            var responseBodySize = response.Content.Headers.ContentLength;
            if (responseBodySize > 0)
            {
                metrics.BytesReceived.Add(responseBodySize.Value, transferTags);
            }

            if (captureBodies)
            {
                var responseCapture = await CaptureBodyAsync(response.Content, cancellationToken);
                if (responseCapture.Replacement is not null)
                {
                    response.Content = responseCapture.Replacement;
                }

                AddProperty(scope, ResponseBodyProperty,
                    RedactBody(request, responseCapture, response.Content.Headers.ContentType?.MediaType, isResponse: true));
            }

            AddProperty(scope, EksenSemanticConventions.HttpResponseStatusCode, statusCode);
            AddProperty(scope, "http.response.body.size", responseBodySize);
            AddProperty(scope, DurationProperty, (long)elapsed.TotalMilliseconds);
            AddHeaderProperties(scope, "http.response.header.", response.Headers, response.Content.Headers);

            using (logger.BeginScope(scope))
            {
                logger.Log(
                    response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                    "Outbound HTTP {http.request.method} {url.full} returned {http.response.status_code} in {eksen.http.client.duration_ms} ms (target {eksen.outbound.target})",
                    method,
                    loggedUrl,
                    statusCode,
                    (long)elapsed.TotalMilliseconds,
                    target);
            }

            return response;
        }
        catch (Exception exception)
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            var errorType = ClassifyError(
                exception,
                cancellationToken,
                Stopwatch.GetElapsedTime(handlerStartTimestamp),
                ResolveTimeout(request));

            var failedTags = transferTags;
            failedTags.Add(EksenSemanticConventions.ErrorType, errorType);

            metrics.Requests.Add(1, failedTags);
            metrics.RequestDuration.Record(elapsed.TotalSeconds, transferTags);

            AddProperty(scope, EksenSemanticConventions.ErrorType, errorType);
            AddProperty(scope, DurationProperty, (long)elapsed.TotalMilliseconds);

            using (logger.BeginScope(scope))
            {
                logger.LogError(
                    exception,
                    "Outbound HTTP {http.request.method} {url.full} failed with {error.type} after {eksen.http.client.duration_ms} ms (target {eksen.outbound.target})",
                    method,
                    loggedUrl,
                    errorType,
                    (long)elapsed.TotalMilliseconds,
                    target);
            }

            throw;
        }
        finally
        {
            metrics.InFlight.Add(-1, inFlightTags);
        }
    }

    public static string ClassifyError(Exception exception, CancellationToken cancellationToken)
    {
        return ClassifyError(exception, cancellationToken, elapsed: TimeSpan.Zero, timeout: null);
    }

    /// <summary>
    /// Classifies a failed call as <c>timeout</c>, <c>canceled</c>, <c>connection_error</c> or the exception type
    /// name. <see cref="HttpClient.Timeout"/> is linked into the token the handler receives, so a canceled token
    /// whose call ran for at least <paramref name="timeout"/> is a timeout, not a caller cancellation.
    /// </summary>
    public static string ClassifyError(Exception exception, CancellationToken cancellationToken, TimeSpan elapsed, TimeSpan? timeout)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (IsTimeoutException(exception))
        {
            return "timeout";
        }

        if (exception is OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                return "timeout";
            }

            return HasReachedTimeout(elapsed, timeout)
                ? "timeout"
                : "canceled";
        }

        if (exception is HttpRequestException)
        {
            return "connection_error";
        }

        return exception.GetType().Name;
    }

    private static bool IsTimeoutException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException)
            {
                return true;
            }

            // Polly's TimeoutRejectedException, matched by name so this package does not depend on Polly.
            if (current.GetType().Name == "TimeoutRejectedException")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReachedTimeout(TimeSpan elapsed, TimeSpan? timeout)
    {
        if (timeout is not { } limit || limit <= TimeSpan.Zero || limit == Timeout.InfiniteTimeSpan)
        {
            return false;
        }

        // HttpClient starts its timer before this handler runs; allow for that gap and for timer resolution.
        var tolerance = TimeSpan.FromTicks(Math.Min(limit.Ticks / 20, TimeSpan.FromSeconds(1).Ticks));
        return elapsed >= limit - tolerance;
    }

    private TimeSpan? ResolveTimeout(HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(OutboundRequestOptions.Timeout, out var perRequest))
        {
            return perRequest;
        }

        return clientTimeout;
    }

    private bool ShouldCaptureBodies(HttpRequestMessage request)
    {
        if (!telemetryOptions.CurrentValue.CaptureOutboundBodies)
        {
            return false;
        }

        if (request.Options.TryGetValue(OutboundRequestOptions.CaptureBodies, out var perRequest))
        {
            return perRequest;
        }

        return clientName is not null && bodyCaptureOptions.CurrentValue.Clients.Contains(clientName);
    }

    private string ResolveTarget(HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(OutboundRequestOptions.Target, out var explicitTarget)
            && !string.IsNullOrWhiteSpace(explicitTarget))
        {
            return explicitTarget;
        }

        if (!string.IsNullOrWhiteSpace(clientName))
        {
            return clientName;
        }

        return ResolveServerAddress(request.RequestUri);
    }

    private string RedactBody(HttpRequestMessage request, BodyCapture capture, string? mediaType, bool isResponse)
    {
        if (!capture.IsContent)
        {
            return capture.Text;
        }

        var redacted = SensitiveBodyRedactor.Redact(capture.Text, mediaType);
        return bodyRedactor.Redact(request, redacted, isResponse);
    }

    private void AddRequestProperties(
        Dictionary<string, object?> scope,
        HttpRequestMessage request,
        string target,
        string method,
        Uri? requestUri,
        string? loggedUrl,
        long? requestBodySize,
        string? requestBody)
    {
        AddProperty(scope, EksenSemanticConventions.EksenOutboundTarget, target);
        AddProperty(scope, EksenSemanticConventions.HttpRequestMethod, method);
        AddProperty(scope, EksenSemanticConventions.UrlFull, loggedUrl);
        AddProperty(scope, "url.scheme", requestUri?.IsAbsoluteUri == true ? requestUri.Scheme : null);
        AddProperty(scope, EksenSemanticConventions.ServerAddress, requestUri?.IsAbsoluteUri == true ? requestUri.Host : null);
        AddProperty(scope, EksenSemanticConventions.ServerPort, requestUri?.IsAbsoluteUri == true ? requestUri.Port : null);
        AddProperty(scope, "network.protocol.version", request.Version.ToString());
        AddProperty(scope, "user_agent.original", request.Headers.UserAgent.ToString());
        AddProperty(scope, "http.request.body.size", requestBodySize);
        AddProperty(scope, RequestBodyProperty, requestBody);
        AddHeaderProperties(scope, "http.request.header.", request.Headers, request.Content?.Headers);
    }

    private void AddHeaderProperties(
        Dictionary<string, object?> scope,
        string prefix,
        HttpHeaders? messageHeaders,
        HttpContentHeaders? contentHeaders)
    {
        AppendHeaderProperties(scope, prefix, messageHeaders);
        AppendHeaderProperties(scope, prefix, contentHeaders);
    }

    private void AppendHeaderProperties(Dictionary<string, object?> scope, string prefix, HttpHeaders? headers)
    {
        if (headers is null)
        {
            return;
        }

        foreach (var header in headers.NonValidated)
        {
            var value = header.Value.ToString();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            AddProperty(scope, prefix + header.Key.ToLowerInvariant(), headerRedactor.Redact(header.Key, value));
        }
    }

    private static void AddProperty(Dictionary<string, object?> scope, string name, object? value)
    {
        if (value is null || (value is string text && text.Length == 0))
        {
            return;
        }

        scope[name] = value is string stringValue
            ? TelemetryValueRedactor.Redact(name, stringValue)
            : value;
    }

    private static string ResolveServerAddress(Uri? requestUri)
    {
        var host = requestUri?.IsAbsoluteUri == true ? requestUri.Host : null;

        return string.IsNullOrWhiteSpace(host)
            ? "unknown"
            : host.ToLowerInvariant();
    }

    private static async Task<BodyCapture> CaptureBodyAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return BodyCapture.FromText("[empty]");
        }

        var contentLength = content.Headers.ContentLength;
        var mediaType = content.Headers.ContentType?.MediaType;

        if (contentLength == 0)
        {
            return BodyCapture.FromText("[empty]");
        }

        if (!IsReadableMediaType(mediaType))
        {
            return BodyCapture.FromText(contentLength is { } size
                ? $"[binary content ({size} bytes)]"
                : "[binary content]");
        }

        if (IsIncrementallyConsumedMediaType(mediaType))
        {
            return BodyCapture.FromText("[stream not captured]");
        }

        if (contentLength is > MaxReadableBodyBytes)
        {
            return BodyCapture.FromText($"[skipped {contentLength} bytes]");
        }

        try
        {
            return await BufferAndCaptureAsync(content, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return BodyCapture.FromText($"[unreadable {exception.GetType().Name}]");
        }
    }

    private static async Task<BodyCapture> BufferAndCaptureAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var encoding = ResolveEncoding(content.Headers.ContentType?.CharSet);
        var source = await content.ReadAsStreamAsync(cancellationToken);

        if (source.CanSeek)
        {
            var resumePosition = source.Position;
            var rewindable = await ReadAtMostAsync(source, MaxReadableBodyBytes, cancellationToken);
            source.Position = resumePosition;

            return Describe(rewindable.Bytes, encoding, replacement: null);
        }

        var consumed = await ReadAtMostAsync(source, MaxReadableBodyBytes, cancellationToken);

        HttpContent replacement;
        if (consumed.ReachedEnd)
        {
            replacement = new ByteArrayContent(consumed.Bytes);
            await source.DisposeAsync();
        }
        else
        {
            replacement = new StreamContent(new ConsumedPrefixStream(consumed.Bytes, source));
        }

        foreach (var header in content.Headers.NonValidated)
        {
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return Describe(consumed.Bytes, encoding, replacement);
    }

    private static async Task<(byte[] Bytes, bool ReachedEnd)> ReadAtMostAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        while (buffer.Length < maxBytes)
        {
            var readable = (int)Math.Min(chunk.Length, maxBytes - buffer.Length);
            var read = await source.ReadAsync(chunk.AsMemory(0, readable), cancellationToken);
            if (read == 0)
            {
                return (buffer.ToArray(), ReachedEnd: true);
            }

            buffer.Write(chunk, 0, read);
        }

        return (buffer.ToArray(), ReachedEnd: false);
    }

    private static BodyCapture Describe(byte[] bytes, Encoding encoding, HttpContent? replacement)
    {
        if (bytes.Length == 0)
        {
            return new BodyCapture("[empty]", replacement, IsContent: false);
        }

        var text = encoding.GetString(bytes);
        if (text.Length > MaxLoggedBodyChars)
        {
            text = text[..MaxLoggedBodyChars] + TruncationMarker;
        }

        return new BodyCapture(text, replacement, IsContent: true);
    }

    private static bool IsIncrementallyConsumedMediaType(string? mediaType)
    {
        return string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadableMediaType(string? mediaType)
    {
        if (string.IsNullOrEmpty(mediaType))
        {
            return false;
        }

        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (mediaType.EndsWith("/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (mediaType.EndsWith("/xml", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private readonly record struct BodyCapture(string Text, HttpContent? Replacement, bool IsContent)
    {
        public static BodyCapture FromText(string text)
        {
            return new BodyCapture(text, Replacement: null, IsContent: false);
        }
    }

    private sealed class ConsumedPrefixStream(byte[] prefix, Stream remainder) : Stream
    {
        private int _prefixOffset;

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (_prefixOffset < prefix.Length)
            {
                return CopyPrefix(buffer);
            }

            return remainder.Read(buffer);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_prefixOffset < prefix.Length)
            {
                return CopyPrefix(buffer.Span);
            }

            return await remainder.ReadAsync(buffer, cancellationToken);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask DisposeAsync()
        {
            await remainder.DisposeAsync();
            await base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                remainder.Dispose();
            }

            base.Dispose(disposing);
        }

        private int CopyPrefix(Span<byte> buffer)
        {
            var count = Math.Min(buffer.Length, prefix.Length - _prefixOffset);
            prefix.AsSpan(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;

            return count;
        }
    }
}
