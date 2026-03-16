using Eksen.DistributedLocks;
using Eksen.DistributedLocks.PostgreSql;
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

            services.TryAddSingleton<IDistributedLockProvider, PostgreSqlDistributedLockProvider>();

            return builder;
        }
    }
}
