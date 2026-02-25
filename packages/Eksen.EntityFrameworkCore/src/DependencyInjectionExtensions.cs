using Eksen.EntityFrameworkCore;
using Eksen.UnitOfWork;

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