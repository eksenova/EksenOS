using Eksen.DistributedLocks;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddDistributedLocks(
        this IEksenBuilder builder,
        Action<IEksenDistributedLocksBuilder>? configureAction = null)
    {
        if (configureAction != null)
        {
            var locksBuilder = new EksenDistributedLocksBuilder(builder);
            configureAction(locksBuilder);
        }

        return builder;
    }
}

public interface IEksenDistributedLocksBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenDistributedLocksBuilder Configure(Action<EksenDistributedLockOptions> configureOptions);
}

public sealed class EksenDistributedLocksBuilder(IEksenBuilder eksenBuilder) : IEksenDistributedLocksBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenDistributedLocksBuilder Configure(Action<EksenDistributedLockOptions> configureOptions)
    {
        this.Services.Configure(configureOptions);
        return this;
    }
}

public static class EksenDistributedLocksBuilderExtensions
{
    extension(IEksenDistributedLocksBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }
    }
}
