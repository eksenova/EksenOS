using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using Shouldly;

namespace Eksen.Scalar.Tests;

public class DocsAuthThemeAndImpersonationTests
{
    private static void ConfigureDocsAuth(ScalarDocsAuthOptions options)
    {
        options.AuthorizeEndpoint = "https://id.example.test/realms/main/protocol/openid-connect/auth";
        options.TokenEndpoint = "https://id.example.test/realms/main/protocol/openid-connect/token";
        options.ClientId = "docs-client";
        options.DocumentPathPrefix = "/openapi/internal";
        options.Title = "Internal reference";
        options.Text["signIn"] = "Continue";
    }

    [Fact]
    public async Task Docs_Auth_Should_Inject_Provider_Endpoints_Client_And_Text()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar.WithEksenDocsAuthPlugin(ConfigureDocsAuth));

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain("__eksenScalarDocsAuthConfig");
        html.ShouldContain("\"authorizeUrl\":\"https://id.example.test/realms/main/protocol/openid-connect/auth\"");
        html.ShouldContain("\"tokenUrl\":\"https://id.example.test/realms/main/protocol/openid-connect/token\"");
        html.ShouldContain("\"clientId\":\"docs-client\"");
        html.ShouldContain("\"callbackPath\":\"" + EksenScalarExtensions.DefaultOAuthCallbackPath + "\"");
        html.ShouldContain("\"signIn\":\"Continue\"");
        html.ShouldNotContain("grant_type', 'password'");
    }

    [Fact]
    public void Docs_Auth_Should_Require_Provider_Endpoints_And_Client()
    {
        var scalar = new ScalarOptions();

        Should.Throw<ArgumentException>(() => scalar.WithEksenDocsAuthPlugin(options => options.ClientId = "docs-client"));
    }

    [Fact]
    public async Task Dock_Should_Be_Injected_Once_For_Docs_Auth_And_Impersonation()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar
            .WithEksenDocsAuthPlugin(ConfigureDocsAuth)
            .WithEksenImpersonationPlugin());

        var html = await host.GetReferenceHtmlAsync();

        html.Split("window.__eksenScalarDock = { slot };").Length.ShouldBe(2);
    }

    [Fact]
    public async Task Header_Impersonation_Should_Inject_Mode_Header_And_Subject_Mapping()
    {
        await using var host = await ScalarTestHost.StartAsync(scalar => scalar.WithEksenImpersonationPlugin(options =>
        {
            options.Mode = ScalarImpersonationMode.Header;
            options.HeaderName = "X-Impersonate-User";
            options.SubjectsEndpoint = "/api/administration/impersonation/users";
            options.SearchParameter = "search";
            options.NameField = "displayName";
            options.DetailField = "email";
            options.HostClaim = null;
        }));

        var html = await host.GetReferenceHtmlAsync();

        html.ShouldContain("\"mode\":\"header\"");
        html.ShouldContain("\"headerName\":\"X-Impersonate-User\"");
        html.ShouldContain("\"subjectsEndpoint\":\"/api/administration/impersonation/users\"");
        html.ShouldContain("\"searchParameter\":\"search\"");
        html.ShouldContain("\"detailField\":\"email\"");
        html.ShouldContain("\"hostClaim\":null");
    }

    [Fact]
    public void Theme_Should_Replace_Builtin_Theme_And_Pin_Light_Mode_Without_Dark_Palette()
    {
        var scalar = new ScalarOptions().WithEksenTheme(theme =>
        {
            theme.BodyFont = "\"Source Sans 3\", sans-serif";
            theme.Radius = "6px";
            theme.Light.Accent = "#5e35b1";
            theme.FontFaces.Add(new ScalarFontFace("Source Sans 3", "/docs/fonts/source-sans-3-400.woff2", 400));
        });

        scalar.Theme.ShouldBe(ScalarTheme.None);
        scalar.ForceThemeMode.ShouldBe(ThemeMode.Light);
        scalar.HideDarkModeToggle.ShouldBe(true);
        scalar.DefaultFonts.ShouldBe(false);
        scalar.CustomCss.ShouldNotBeNull();
        scalar.CustomCss.ShouldContain("@font-face{font-family:'Source Sans 3'");
        scalar.CustomCss.ShouldContain(".light-mode{--scalar-color-1:#212121;");
        scalar.CustomCss.ShouldContain("--scalar-color-accent:#5e35b1;");
        scalar.CustomCss.ShouldContain("--scalar-radius:6px;");
        scalar.CustomCss.ShouldNotContain(".dark-mode{");
    }

    [Fact]
    public void Theme_Should_Keep_Mode_Toggle_With_Dark_Palette()
    {
        var scalar = new ScalarOptions().WithEksenTheme(theme => theme.Dark = new ScalarPalette { Surface = "#121212" });

        scalar.ForceThemeMode.ShouldBeNull();
        scalar.CustomCss!.ShouldContain(".dark-mode{");
        scalar.CustomCss!.ShouldContain("--scalar-background-1:#121212;");
    }

    [Fact]
    public void Theme_Should_Reject_Values_That_Break_Out_Of_A_Declaration()
    {
        Should.Throw<ArgumentException>(() => new ScalarOptions()
            .WithEksenTheme(theme => theme.Light.Accent = "red;}body{display:none"));
    }

    [Fact]
    public async Task OAuth_Callback_Should_Serve_An_Uncached_Page_That_Posts_The_Code_Back()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRouting();
        await using var app = builder.Build();
        app.MapEksenScalarOAuthCallback();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var response = await client.GetAsync(EksenScalarExtensions.DefaultOAuthCallbackPath + "?code=abc&state=xyz", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        html.ShouldContain("type: 'eksen-docs-oauth'");
        html.ShouldContain("window.opener.postMessage(message, window.location.origin)");
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
