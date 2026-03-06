using Eksen.Auditing.AspNetCore;
using Microsoft.AspNetCore.Builder;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenAuditingBuilder AddAspNetCoreIntegration(
        this IEksenAuditingBuilder builder)
    {
        return builder;
    }
}

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEksenAuditing(
        this IApplicationBuilder app)
    {
        app.UseMiddleware<AuditingMiddleware>();
        return app;
    }
}
