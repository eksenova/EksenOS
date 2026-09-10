using System.Net;
using System.Text;
using Eksen.Captcha;
using Eksen.Captcha.Cloudflare;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Eksen.Captcha.Cloudflare.Tests;

public sealed class CloudflareTurnstileCaptchaProviderTests
{
    private sealed class StubHandler(HttpStatusCode statusCode, string payload) : HttpMessageHandler
    {
        public string? CapturedRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }

    private static CloudflareTurnstileCaptchaProvider CreateProvider(
        StubHandler handler,
        CloudflareTurnstileOptions? options = null)
    {
        return new CloudflareTurnstileCaptchaProvider(
            new StubHttpClientFactory(handler),
            new StaticOptionsMonitor<CloudflareTurnstileOptions>(
                options ?? new CloudflareTurnstileOptions { SecretKey = "secret" }),
            NullLogger<CloudflareTurnstileCaptchaProvider>.Instance);
    }

    [Fact]
    public async Task Successful_Verification_Should_Be_Mapped()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """
            {"success":true,"hostname":"app.eksengold.com","challenge_ts":"2026-09-10T08:00:00Z"}
            """);

        var provider = CreateProvider(handler);

        var result = await provider.VerifyAsync(
            CaptchaResponseToken.Create("token"),
            IPAddress.Parse("198.51.100.7"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("app.eksengold.com", result.Hostname);
        Assert.NotNull(result.ChallengeTime);
    }

    [Fact]
    public async Task Failed_Verification_Should_Surface_Error_Codes()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """
            {"success":false,"error-codes":["invalid-input-response","timeout-or-duplicate"]}
            """);

        var provider = CreateProvider(handler);

        var result = await provider.VerifyAsync(
            CaptchaResponseToken.Create("token"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.ErrorCodes.Length);
        Assert.Contains("invalid-input-response", result.ErrorCodes);
    }

    [Fact]
    public async Task Transport_Failure_Should_Not_Throw()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "boom");
        var provider = CreateProvider(handler);

        var result = await provider.VerifyAsync(
            CaptchaResponseToken.Create("token"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Contains("eksen-transport-failure", result.ErrorCodes);
    }

    [Fact]
    public async Task Remote_Ip_Should_Be_Forwarded_To_Cloudflare()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"success":true}""");
        var provider = CreateProvider(handler);

        await provider.VerifyAsync(
            CaptchaResponseToken.Create("token"),
            IPAddress.Parse("198.51.100.7"),
            TestContext.Current.CancellationToken);

        Assert.Contains("remoteip=198.51.100.7", handler.CapturedRequestBody);
        Assert.Contains("secret=secret", handler.CapturedRequestBody);
    }

    [Fact]
    public async Task Remote_Ip_Should_Be_Omitted_When_Disabled()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"success":true}""");
        var provider = CreateProvider(handler, new CloudflareTurnstileOptions
        {
            SecretKey = "secret",
            SendRemoteIpAddress = false,
        });

        await provider.VerifyAsync(
            CaptchaResponseToken.Create("token"),
            IPAddress.Parse("198.51.100.7"),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain("remoteip", handler.CapturedRequestBody);
    }
}
