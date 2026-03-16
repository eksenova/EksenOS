using Eksen.EventBus;
using Eksen.EventBus.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public interface IEksenEventBusDashboardBuilder
{
    IEksenEventBusBuilder EventBusBuilder { get; }

    IEksenEventBusDashboardBuilder Configure(Action<EventBusDashboardOptions> configureOptions);

    IEksenEventBusDashboardBuilder UseBasicAuth(string username, string password);

    IEksenEventBusDashboardBuilder UseOpenIdConnect(Action<Eksen.EventBus.Dashboard.OpenIdConnectOptions> configure);

    IEksenEventBusDashboardBuilder UseCustomAuth(
        Func<Eksen.EventBus.Dashboard.HttpContextAccessor, Task<bool>> authorizeFunc);
}

public class EksenEventBusDashboardBuilder(IEksenEventBusBuilder eventBusBuilder) : IEksenEventBusDashboardBuilder
{
    public IEksenEventBusBuilder EventBusBuilder { get; } = eventBusBuilder;

    public IEksenEventBusDashboardBuilder Configure(Action<EventBusDashboardOptions> configureOptions)
    {
        EventBusBuilder.EksenBuilder.Services.Configure(configureOptions);
        return this;
    }

    public IEksenEventBusDashboardBuilder UseBasicAuth(string username, string password)
    {
        EventBusBuilder.EksenBuilder.Services.Configure<EventBusDashboardOptions>(o =>
        {
            o.Auth.AuthMode = EventBusDashboardAuthMode.BasicAuth;
            o.Auth.Username = username;
            o.Auth.Password = password;
        });
        return this;
    }

    public IEksenEventBusDashboardBuilder UseOpenIdConnect(
        Action<Eksen.EventBus.Dashboard.OpenIdConnectOptions> configure)
    {
        EventBusBuilder.EksenBuilder.Services.Configure<EventBusDashboardOptions>(o =>
        {
            o.Auth.AuthMode = EventBusDashboardAuthMode.OpenIdConnect;
            o.Auth.OpenIdConnect ??= new Eksen.EventBus.Dashboard.OpenIdConnectOptions();
            configure(o.Auth.OpenIdConnect);
        });

        EventBusBuilder.EksenBuilder.Services.AddAuthentication()
            .AddOpenIdConnect("EksenEventBusDashboard", oidcOptions =>
            {
                var sp = EventBusBuilder.EksenBuilder.Services.BuildServiceProvider();
                var dashOptions = sp.GetRequiredService<IOptions<EventBusDashboardOptions>>().Value;
                var oidc = dashOptions.Auth.OpenIdConnect!;

                oidcOptions.Authority = oidc.Authority;
                oidcOptions.ClientId = oidc.ClientId;
                oidcOptions.ClientSecret = oidc.ClientSecret;
                oidcOptions.CallbackPath = oidc.CallbackPath;

                foreach (var scope in oidc.Scopes)
                    oidcOptions.Scope.Add(scope);
            });

        return this;
    }

    public IEksenEventBusDashboardBuilder UseCustomAuth(
        Func<Eksen.EventBus.Dashboard.HttpContextAccessor, Task<bool>> authorizeFunc)
    {
        EventBusBuilder.EksenBuilder.Services.Configure<EventBusDashboardOptions>(o =>
        {
            o.Auth.AuthMode = EventBusDashboardAuthMode.Custom;
            o.Auth.CustomAuthorize = authorizeFunc;
        });
        return this;
    }
}

public static class DashboardDependencyInjectionExtensions
{
    public static IEksenEventBusBuilder AddDashboard(
        this IEksenEventBusBuilder builder,
        Action<IEksenEventBusDashboardBuilder>? configureAction = null)
    {
        var services = builder.EksenBuilder.Services;

        if (configureAction != null)
        {
            var dashboardBuilder = new EksenEventBusDashboardBuilder(builder);
            configureAction(dashboardBuilder);
        }

        return builder;
    }

    public static IApplicationBuilder UseEksenEventBusDashboard(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices
            .GetRequiredService<IOptions<EventBusDashboardOptions>>().Value;

        var routePrefix = options.RoutePrefix.TrimStart('/');

        app.Map($"/{routePrefix}", dashApp =>
        {
            dashApp.UseMiddleware<DashboardAuthMiddleware>();

            dashApp.UseRouting();

            dashApp.UseEndpoints(endpoints =>
            {
                endpoints.MapEventBusDashboardApi(routePrefix);
            });

            var assembly = typeof(DashboardDependencyInjectionExtensions).Assembly;
            var fileProvider = new EmbeddedFileProvider(assembly, "Eksen.EventBus.Dashboard.wwwroot");

            dashApp.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = fileProvider
            });

            dashApp.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = ""
            });

            dashApp.Run(async context =>
            {
                if (!context.Request.Path.HasValue || !context.Request.Path.Value.Contains('.'))
                {
                    context.Response.ContentType = "text/html";
                    var file = fileProvider.GetFileInfo("index.html");
                    if (file.Exists)
                    {
                        await using var stream = file.CreateReadStream();
                        await stream.CopyToAsync(context.Response.Body);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                    }
                }
            });
        });

        return app;
    }
}
