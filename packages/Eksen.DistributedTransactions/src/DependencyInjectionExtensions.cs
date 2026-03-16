using Eksen.DistributedTransactions;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddDistributedTransactions(
        this IEksenBuilder builder,
        Action<IEksenDistributedTransactionsBuilder>? configureAction = null)
    {
        var services = builder.Services;

        services.TryAddScoped<IDistributedTransactionManager, DistributedTransactionManager>();

        if (configureAction != null)
        {
            var txBuilder = new EksenDistributedTransactionsBuilder(builder);
            configureAction(txBuilder);
        }

        return builder;
    }
}

public interface IEksenDistributedTransactionsBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public sealed class EksenDistributedTransactionsBuilder(IEksenBuilder eksenBuilder)
    : IEksenDistributedTransactionsBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}

public static class EksenDistributedTransactionsBuilderExtensions
{
    extension(IEksenDistributedTransactionsBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}

public static class DistributedTransactionsUnitOfWorkExtensions
{
    extension(IEksenUnitOfWorkBuilder builder)
    {
        public IEksenUnitOfWorkBuilder UseDistributedTransactions(
            Action<DistributedUnitOfWorkOptions>? configureOptions = null)
        {
            var services = builder.Services;

            services.TryAddScoped<IDistributedTransactionManager, DistributedTransactionManager>();

            services.RemoveAll<IUnitOfWorkManager>();
            services.AddScoped<IUnitOfWorkManager, DistributedUnitOfWorkManager>();

            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            return builder;
        }
    }
}
