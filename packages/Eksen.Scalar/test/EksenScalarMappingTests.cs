using Scalar.AspNetCore;
using Shouldly;

namespace Eksen.Scalar.Tests;

/// <summary>
/// HTTP-level tests over the rendered Scalar HTML. Each plugin is injected as an inline ES-module
/// script on the document head, so a plugin's presence is detected by its <c>window</c> config global
/// (and configured values) appearing in the page, and its absence by that marker being missing.
/// </summary>
public class EksenScalarMappingTests
{
    private const string LogoMarker = "__eksenScalarLogoConfig";
    private const string AutofillMarker = "__eksenScalarAutofillLoaded";
    private const string TokenBodyMarker = "__eksenScalarTokenBodyConfig";
    private const string ImpersonationMarker = "__eksenScalarImpersonationConfig";
    private const string InternalAuthMarker = "__eksenScalarInternalAuthConfig";

    #region No plugins: a plain reference maps and injects nothing

    [Fact]
    public async Task Plain_Reference_Should_Be_Served()
    {
        await using var host = await ScalarTestHost.StartAsync(_ => { });

        var response = await host.Client.GetAsync("/scalar");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Plain_Reference_Should_Not_Inject_Any_Plugin()
    {
        await using var host = await ScalarTestHost.StartAsync(_ => { });

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldNotContain(LogoMarker);
        html.ShouldNotContain(AutofillMarker);
        html.ShouldNotContain(TokenBodyMarker);
        html.ShouldNotContain(ImpersonationMarker);
        html.ShouldNotContain(InternalAuthMarker);
    }

    #endregion

    #region Individual plugins inject their script and configured values

    [Fact]
    public async Task Logo_Plugin_Should_Inject_Configured_Logo_Url()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenLogoPlugin(logo =>
            {
                logo.LogoUrl = "https://cdn.example.com/logo.png";
                logo.FooterText = "(c) {year} Example";
            }));

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain(LogoMarker);
        html.ShouldContain("https://cdn.example.com/logo.png");
        html.ShouldContain("(c) {year} Example");
    }

    [Fact]
    public async Task Autofill_Plugin_Should_Inject_Its_Module()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar.WithEksenAutofillPlugin());

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain(AutofillMarker);
    }

    [Fact]
    public async Task TokenBody_Plugin_Should_Inject_Configured_Endpoint()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenTokenBodyPlugin(token => token.TokenEndpoint = "/oauth/token"));

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain(TokenBodyMarker);
        html.ShouldContain("/oauth/token");
    }

    [Fact]
    public async Task Impersonation_Plugin_Should_Inject_Configured_Values()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenImpersonationPlugin(impersonation =>
            {
                impersonation.TenantsEndpoint = "/api/orgs";
                impersonation.GrantType = "org_impersonation";
            }));

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain(ImpersonationMarker);
        html.ShouldContain("/api/orgs");
        html.ShouldContain("org_impersonation");
    }

    [Fact]
    public async Task InternalAuth_Plugin_Should_Inject_Configured_Host_Claim()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenInternalAuthPlugin(auth => auth.HostClaim = "is_admin"));

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain(InternalAuthMarker);
        html.ShouldContain("is_admin");
    }

    #endregion

    #region Composition: plugins are independent and chain freely

    [Fact]
    public async Task Multiple_Plugins_Should_All_Be_Injected()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenLogoPlugin(logo => logo.LogoUrl = "https://cdn.example.com/logo.png")
            .WithEksenImpersonationPlugin());

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain(LogoMarker);
        html.ShouldContain(ImpersonationMarker);
    }

    [Fact]
    public async Task Enabling_One_Plugin_Should_Not_Inject_The_Others()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenLogoPlugin(logo => logo.LogoUrl = "https://cdn.example.com/logo.png"));

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain(LogoMarker);
        html.ShouldNotContain(ImpersonationMarker);
        html.ShouldNotContain(InternalAuthMarker);
        html.ShouldNotContain(TokenBodyMarker);
    }

    [Fact]
    public async Task Plugins_Should_Be_Stable_Across_Repeated_Requests()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenLogoPlugin(logo => logo.LogoUrl = "https://cdn.example.com/logo.png"));

        const string assignment = "window." + LogoMarker + " = {";

        var first = await host.GetReferenceHtmlAsync();
        var second = await host.GetReferenceHtmlAsync();

        // The config global is assigned exactly once per render, and the page is identical across
        // requests, so the per-request options snapshot never accumulates injected scripts.
        CountOccurrences(first, assignment).ShouldBe(1);
        second.ShouldBe(first);
    }

    #endregion

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
