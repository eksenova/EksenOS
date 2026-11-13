using System.Diagnostics.Metrics;
using Eksen.EntityFrameworkCore;
using Eksen.Telemetry;
using Eksen.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddEntityFrameworkCore(
        this IEksenBuilder eksenBuilder,
        Action<IEksenEntityFrameworkCoreBuilder>? configureAction = null)
    {
        var services = eksenBuilder.Services;

        services.AddScoped<IUnitOfWorkProvider, EfCoreUnitOfWorkProvider>();
        services.AddScoped<IDbContextTracker, DbContextTracker>();

        services.AddMetrics();
        services.AddOptions<EksenDbCommandMetricsOptions>();
        services.TryAddSingleton(provider => new DbCommandMetricsInterceptor(
            provider.GetService<IMeterFactory>(),
            provider.GetService<IOptions<EksenDbCommandMetricsOptions>>()));
        services.AddEksenTelemetryMeter(EksenTelemetry.EntityFrameworkCore);

        if (configureAction != null)
        {
            var builder = new EksenEntityFrameworkCoreBuilder(eksenBuilder);
            configureAction(builder);
        }

        return eksenBuilder;
    }
}

public sealed class EksenEntityFrameworkCoreBuilder(IEksenBuilder builder) : IEksenEntityFrameworkCoreBuilder
{
    public IEksenBuilder EksenBuilder { get; } = builder;
}

public interface IEksenEntityFrameworkCoreBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public static class EksenEntityFrameworkCoreBuilderExtensions
{
    extension(IEksenEntityFrameworkCoreBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}

public static class EksenDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="DbCommandMetricsInterceptor"/> (the instance registered by
    /// <c>AddEntityFrameworkCore</c>, or a new one bound to the container's <see cref="IMeterFactory"/>).
    /// </summary>
    public static DbContextOptionsBuilder UseEksenCommandMetrics(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var interceptor = serviceProvider.GetService<DbCommandMetricsInterceptor>()
                          ?? new DbCommandMetricsInterceptor(
                              serviceProvider.GetService<IMeterFactory>(),
                              serviceProvider.GetService<IOptions<EksenDbCommandMetricsOptions>>());

        return optionsBuilder.AddInterceptors(interceptor);
    }

    /// <inheritdoc cref="UseEksenCommandMetrics(DbContextOptionsBuilder, IServiceProvider)"/>
    public static DbContextOptionsBuilder<TContext> UseEksenCommandMetrics<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        return (DbContextOptionsBuilder<TContext>)UseEksenCommandMetrics((DbContextOptionsBuilder)optionsBuilder, serviceProvider);
    }
}
