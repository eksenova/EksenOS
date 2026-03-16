namespace Eksen.EventBus.Dashboard;

public class EventBusDashboardOptions
{
    public string RoutePrefix { get; set; } = "eksen-eventbus";

    public string Title { get; set; } = "Eksen EventBus Dashboard";

    public EventBusDashboardAuthOptions Auth { get; set; } = new();
}

public class EventBusDashboardAuthOptions
{
    public EventBusDashboardAuthMode AuthMode { get; set; } = EventBusDashboardAuthMode.None;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public OpenIdConnectOptions? OpenIdConnect { get; set; }

    public Func<HttpContextAccessor, Task<bool>>? CustomAuthorize { get; set; }
}

public enum EventBusDashboardAuthMode
{
    None,
    BasicAuth,
    OpenIdConnect,
    Custom
}

public class OpenIdConnectOptions
{
    public string Authority { get; set; } = null!;

    public string ClientId { get; set; } = null!;

    public string? ClientSecret { get; set; }

    public string[] Scopes { get; set; } = ["openid", "profile"];

    public string CallbackPath { get; set; } = "/eksen-eventbus/auth/callback";
}

public class HttpContextAccessor
{
    public required Microsoft.AspNetCore.Http.HttpContext HttpContext { get; init; }
}
