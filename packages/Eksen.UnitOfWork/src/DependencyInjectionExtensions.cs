using System.Diagnostics.Metrics;
using Eksen.Telemetry;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public interface IEksenUnitOfWorkBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public class EksenUnitOfWorkBuilder(IEksenBuilder eksenBuilder) : IEksenUnitOfWorkBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

public static class EksenUnitOfWorkBuilderExtensions
{
    extension(IEksenUnitOfWorkBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddUnitOfWork(
        this IEksenBuilder builder,
        Action<IEksenUnitOfWorkBuilder>? configureAction = null
    )
    {
        var services = builder.Services;
        services.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
        services.AddMetrics();
        services.TryAddSingleton(provider => UnitOfWorkTelemetry.For(provider.GetService<IMeterFactory>()));
        services.AddEksenTelemetrySource(EksenTelemetry.UnitOfWork);
        services.AddEksenTelemetryMeter(EksenTelemetry.UnitOfWork);

        if (configureAction != null)
        {
            var uowBuilder = new EksenUnitOfWorkBuilder(builder);
            configureAction(uowBuilder);
        }

        return builder;
    }
}