using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.Telemetry.Tests.Redaction;

public class UrlPathScrubberTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("/0b3f8c3e-7d55-4c35-9c2f-6c1c1c0b6a11/LicenseChecker/check", "/{license}/LicenseChecker/check")]
    [InlineData("/0B3F8C3E-7D55-4C35-9C2F-6C1C1C0B6A11/LicenseValidation/", "/{license}/LicenseValidation/")]
    [InlineData("/0b3f8c3e-7d55-4c35-9c2f-6c1c1c0b6a11/VersionCheck/plugin", "/{license}/VersionCheck/plugin")]
    [InlineData("/interactions/123456789012345678/aW50ZXJhY3Rpb246dG9rZW4/callback", "/interactions/123456789012345678/{token}/callback")]
    [InlineData("/api/v10/webhooks/123456789012345678/abcDEF_ghi-123/messages/@original", "/api/v10/webhooks/123456789012345678/{token}/messages/@original")]
    [InlineData("/api/products/42", "/api/products/42")]
    [InlineData("/0b3f8c3e-7d55-4c35-9c2f-6c1c1c0b6a11/Other/", "/0b3f8c3e-7d55-4c35-9c2f-6c1c1c0b6a11/Other/")]
    public void Applies_Registered_Rules(string path, string expected)
    {
        CreateScrubber().Scrub(path).ShouldBe(expected);
    }

    [Fact]
    public void Rules_Run_In_Registration_Order()
    {
        var services = new ServiceCollection();
        services.AddEksenTelemetryPrimitives();
        services.AddEksenUrlPathScrubRule("^/a/", "/b/");
        services.AddEksenUrlPathScrubRule("^/b/", "/c/");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IUrlPathScrubber>().Scrub("/a/x").ShouldBe("/c/x");
    }

    [Fact]
    public void ScrubUrl_Should_Scrub_The_Path_Redact_The_Query_And_Drop_User_Info()
    {
        var scrubber = CreateScrubber();

        var scrubbed = scrubber.ScrubUrl(new Uri("https://user:pass@discord.com:8443/api/webhooks/123/abc-def?wait=true&token=xyz"));

        scrubbed.ShouldBe("https://discord.com:8443/api/webhooks/123/{token}?wait=true&token=[redacted]");
    }

    [Fact]
    public void ScrubUrl_Should_Handle_Relative_Text()
    {
        CreateScrubber().ScrubUrl("/0b3f8c3e-7d55-4c35-9c2f-6c1c1c0b6a11/LicenseChecker/?apikey=1")
            .ShouldBe("/{license}/LicenseChecker/?apikey=[redacted]");
    }

    [Fact]
    public void Without_Rules_The_Path_Is_Unchanged()
    {
        new DefaultUrlPathScrubber([]).Scrub("/any/path").ShouldBe("/any/path");
    }

    private static IUrlPathScrubber CreateScrubber()
    {
        var services = new ServiceCollection();
        services.AddEksenTelemetryPrimitives();
        services.AddEksenUrlPathScrubRule(@"^/[0-9a-fA-F-]{36}/(LicenseChecker|LicenseValidation|VersionCheck)/", "/{license}/$1/");
        services.AddEksenUrlPathScrubRule(@"/interactions/(\d+)/[^/]+/", "/interactions/$1/{token}/");
        services.AddEksenUrlPathScrubRule(@"/webhooks/(\d+)/[^/]+", "/webhooks/$1/{token}");

        return services.BuildServiceProvider().GetRequiredService<IUrlPathScrubber>();
    }
}
