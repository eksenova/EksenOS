using System.Diagnostics.Metrics;
using Eksen.DistributedLocks;
using Eksen.DistributedLocks.PostgreSql;
using Eksen.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenDistributedLocksBuilder builder)
    {
        public IEksenDistributedLocksBuilder UsePostgreSql(
            string configSectionPath = PostgreSqlDistributedLockOptions.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            services
                .AddOptions<PostgreSqlDistributedLockOptions>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddMetrics();
            services.TryAddSingleton(provider => DistributedLocksTelemetry.For(provider.GetService<IMeterFactory>()));
            services.TryAddSingleton<IDistributedLockProvider, PostgreSqlDistributedLockProvider>();
            services.AddEksenTelemetryMeter(EksenTelemetry.DistributedLocks);

            return builder;
        }
    }
}
