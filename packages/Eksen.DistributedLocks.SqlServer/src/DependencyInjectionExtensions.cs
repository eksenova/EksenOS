using Eksen.DistributedLocks;
using Eksen.DistributedLocks.SqlServer;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenDistributedLocksBuilder builder)
    {
        public IEksenDistributedLocksBuilder UseSqlServer(
            string configSectionPath = SqlServerDistributedLockOptions.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            services
                .AddOptions<SqlServerDistributedLockOptions>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.TryAddSingleton<IDistributedLockProvider, SqlServerDistributedLockProvider>();

            return builder;
        }
    }
}
