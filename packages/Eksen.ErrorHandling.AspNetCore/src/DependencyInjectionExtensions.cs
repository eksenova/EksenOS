using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Eksen.ErrorHandling.AspNetCore;
using Eksen.Telemetry;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenErrorHandlingBuilder AddAspNetCoreSupport(
        this IEksenErrorHandlingBuilder builder)
    {
        var services = builder.Services;

        services.AddExceptionHandler(_ => { });
        services.AddMetrics();
        services.AddSingleton<IExceptionHandler>(provider =>
            new EksenExceptionHandler(
                provider.GetRequiredService<ILogger<EksenExceptionHandler>>(),
                errorFormatter: null,
                provider.GetService<IMeterFactory>(),
                provider.GetService<IOptionsMonitor<EksenTelemetryOptions>>()));
        services.AddEksenTelemetryMeter(EksenTelemetry.ErrorHandling);

        return builder;
    }
}