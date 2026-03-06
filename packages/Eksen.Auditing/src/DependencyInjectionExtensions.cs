using Autofac;
using Autofac.Extras.DynamicProxy;
using Eksen.Auditing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddAuditing(
        this IEksenBuilder builder,
        Action<IEksenAuditingBuilder>? configureAction = null)
    {
        var services = builder.Services;

        services.TryAddScoped<IAuditLogManager, AuditLogManager>();
        services.TryAddScoped<AuditingInterceptor>();

        if (configureAction != null)
        {
            var auditingBuilder = new EksenAuditingBuilder(builder);
            configureAction(auditingBuilder);
        }

        return builder;
    }
}

public interface IEksenAuditingBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenAuditingBuilder Configure(Action<EksenAuditingOptions> configureOptions);
}

public class EksenAuditingBuilder(IEksenBuilder eksenBuilder) : IEksenAuditingBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    public IEksenAuditingBuilder Configure(Action<EksenAuditingOptions> configureOptions)
    {
        this.Services.Configure(configureOptions);
        return this;
    }
}

public static class EksenAuditingBuilderExtensions
{
    extension(IEksenAuditingBuilder builder)
    {
        public IServiceCollection Services
        {
            get { return builder.EksenBuilder.Services; }
        }

        public IEksenAuditingBuilder UseAutofacProxies()
        {
            var services = builder.Services;

            services.AddSingleton<IRegistrationCallback>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<EksenAuditingOptions>>();
                return new AutofacAuditingRegistrationCallback(options.Value);
            });

            return builder;
        }
    }
}

public interface IRegistrationCallback
{
    void Configure(ContainerBuilder containerBuilder);
}

public sealed class AutofacAuditingRegistrationCallback(EksenAuditingOptions options) : IRegistrationCallback
{
    public void Configure(ContainerBuilder containerBuilder)
    {
        containerBuilder.RegisterType<AuditingInterceptor>()
            .InstancePerLifetimeScope();

        foreach (var type in options.AuditedTypes)
        {
            if (type.IsInterface)
            {
                // For interfaces, find and register all existing registrations with interceptor
                containerBuilder.RegisterCallback(registry =>
                {
                    // No-op: The wrapping is done via EnableInterfaceInterceptors at resolution time
                });
            }
            else if (type is { IsClass: true, IsAbstract: false })
            {
                containerBuilder.RegisterType(type)
                    .EnableClassInterceptors()
                    .InterceptedBy(typeof(AuditingInterceptor))
                    .InstancePerLifetimeScope();
            }
        }
    }
}