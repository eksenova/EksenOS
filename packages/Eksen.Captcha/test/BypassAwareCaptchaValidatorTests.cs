using System.Net;
using Eksen.Captcha;
using Microsoft.Extensions.Options;

namespace Eksen.Captcha.Tests;

public sealed class BypassAwareCaptchaValidatorTests
{
    private sealed class RecordingCaptchaProvider(CaptchaValidationResult? result = null) : ICaptchaProvider
    {
        public int CallCount { get; private set; }

        public IPAddress? LastRemoteIpAddress { get; private set; }

        public Task<CaptchaValidationResult> VerifyAsync(
            CaptchaResponseToken responseToken,
            IPAddress? remoteIpAddress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRemoteIpAddress = remoteIpAddress;
            return Task.FromResult(result ?? CaptchaValidationResult.Success());
        }
    }

    private static BypassAwareCaptchaValidator CreateValidator(
        ICaptchaProvider provider,
        EksenCaptchaOptions options)
    {
        var monitor = new StaticOptionsMonitor<EksenCaptchaOptions>(options);
        return new BypassAwareCaptchaValidator(provider, monitor);
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

    [Fact]
    public async Task Bypass_Token_Should_Short_Circuit_The_Provider()
    {
        var provider = new RecordingCaptchaProvider();
        var validator = CreateValidator(provider, new EksenCaptchaOptions
        {
            BypassTokens = ["let-me-in"],
        });

        var result = await validator.ValidateAsync(
            CaptchaResponseToken.Create("let-me-in"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.True(result.IsBypassed);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Non_Bypass_Token_Should_Reach_The_Provider()
    {
        var provider = new RecordingCaptchaProvider();
        var validator = CreateValidator(provider, new EksenCaptchaOptions
        {
            BypassTokens = ["let-me-in"],
        });

        var result = await validator.ValidateAsync(
            CaptchaResponseToken.Create("a-real-turnstile-token"),
            IPAddress.Parse("198.51.100.7"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.False(result.IsBypassed);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(IPAddress.Parse("198.51.100.7"), provider.LastRemoteIpAddress);
    }

    [Fact]
    public async Task Disabled_Captcha_Should_Bypass_Everything()
    {
        var provider = new RecordingCaptchaProvider();
        var validator = CreateValidator(provider, new EksenCaptchaOptions { Enabled = false });

        var result = await validator.ValidateAsync(
            CaptchaResponseToken.Create("anything"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsBypassed);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task Provider_Failure_Should_Be_Surfaced()
    {
        var provider = new RecordingCaptchaProvider(
            CaptchaValidationResult.Failure("invalid-input-response"));

        var validator = CreateValidator(provider, new EksenCaptchaOptions());

        var result = await validator.ValidateAsync(
            CaptchaResponseToken.Create("bad-token"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal("invalid-input-response", Assert.Single(result.ErrorCodes));
    }

    [Theory]
    [InlineData("let-me-in", "let-me-in", true)]
    [InlineData("let-me-in", "Let-Me-In", false)]
    [InlineData("let-me-in", "let-me-in ", false)]
    [InlineData("let-me-in", "", false)]
    public void Bypass_Match_Should_Be_Exact(string configured, string candidate, bool expected)
    {
        Assert.Equal(expected, BypassAwareCaptchaValidator.IsBypassToken([configured], candidate));
    }

    [Fact]
    public void Empty_Bypass_Entries_Should_Never_Match()
    {
        Assert.False(BypassAwareCaptchaValidator.IsBypassToken(["", "   "], ""));
    }

    [Fact]
    public void Empty_Response_Token_Should_Be_Rejected_By_The_Value_Object()
    {
        Assert.ThrowsAny<Exception>(() => CaptchaResponseToken.Create(" "));
    }
}
