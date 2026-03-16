using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.Dashboard;

public class DashboardAuthMiddleware(
    RequestDelegate next,
    IOptions<EventBusDashboardOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var authOptions = options.Value.Auth;

        var isAuthorized = authOptions.AuthMode switch
        {
            EventBusDashboardAuthMode.None => true,
            EventBusDashboardAuthMode.BasicAuth => ValidateBasicAuth(context, authOptions),
            EventBusDashboardAuthMode.OpenIdConnect => context.User.Identity?.IsAuthenticated == true,
            EventBusDashboardAuthMode.Custom => authOptions.CustomAuthorize != null
                && await authOptions.CustomAuthorize(new HttpContextAccessor { HttpContext = context }),
            _ => false
        };

        if (!isAuthorized)
        {
            if (authOptions.AuthMode == EventBusDashboardAuthMode.BasicAuth)
            {
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"Eksen EventBus Dashboard\"";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (authOptions.AuthMode == EventBusDashboardAuthMode.OpenIdConnect)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Authentication required" });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }

    private static bool ValidateBasicAuth(HttpContext context, EventBusDashboardAuthOptions authOptions)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        var encodedCredentials = authHeader["Basic ".Length..].Trim();
        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(encodedCredentials);
        }
        catch
        {
            return false;
        }

        var credentials = Encoding.UTF8.GetString(decodedBytes);
        var separatorIndex = credentials.IndexOf(':');
        if (separatorIndex < 0)
            return false;

        var username = credentials[..separatorIndex];
        var password = credentials[(separatorIndex + 1)..];

        var expectedUsername = authOptions.Username ?? string.Empty;
        var expectedPassword = authOptions.Password ?? string.Empty;

        return CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(username),
                   Encoding.UTF8.GetBytes(expectedUsername))
               && CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(password),
                   Encoding.UTF8.GetBytes(expectedPassword));
    }
}
