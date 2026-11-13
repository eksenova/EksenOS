using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Eksen.Telemetry.OpenTelemetry.Logging;

/// <summary>
/// Decorates the OpenTelemetry logger provider so the scopes it exports pass through
/// <see cref="RedactingExternalScopeProvider"/>. Log record processors cannot change scopes, so this is where
/// scope values are redacted. Carries the <c>OpenTelemetry</c> alias, so <c>Logging:OpenTelemetry</c>
/// configuration still applies to it.
/// </summary>
[ProviderAlias("OpenTelemetry")]
public sealed class ScopeRedactingLoggerProvider(ILoggerProvider inner) : ILoggerProvider, ISupportExternalScope
{
    public ILoggerProvider Inner
    {
        get { return inner; }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return inner.CreateLogger(categoryName);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        if (inner is ISupportExternalScope supportsScopes)
        {
            supportsScopes.SetScopeProvider(new RedactingExternalScopeProvider(scopeProvider));
        }
    }

    public void Dispose()
    {
        inner.Dispose();
    }

    /// <summary>
    /// Replaces the <see cref="OpenTelemetryLoggerProvider"/> registration with a decorated one.
    /// </summary>
    /// <exception cref="InvalidOperationException">No OpenTelemetry logger provider is registered.</exception>
    internal static void Decorate(IServiceCollection services)
    {
        for (var index = 0; index < services.Count; index++)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType != typeof(ILoggerProvider) || descriptor.IsKeyedService || !IsOpenTelemetryProvider(descriptor))
            {
                continue;
            }

            services[index] = ServiceDescriptor.Describe(
                typeof(ILoggerProvider),
                serviceProvider => new ScopeRedactingLoggerProvider(CreateInner(descriptor, serviceProvider)),
                descriptor.Lifetime);
            return;
        }

        throw new InvalidOperationException(
            "The OpenTelemetry logger provider registration was not found, so exported log scopes could not be redacted.");
    }

    private static bool IsOpenTelemetryProvider(ServiceDescriptor descriptor)
    {
        var implementationType = descriptor.ImplementationType
                                 ?? descriptor.ImplementationInstance?.GetType()
                                 ?? descriptor.ImplementationFactory?.Method.ReturnType;

        return implementationType == typeof(OpenTelemetryLoggerProvider);
    }

    private static ILoggerProvider CreateInner(ServiceDescriptor descriptor, IServiceProvider serviceProvider)
    {
        if (descriptor.ImplementationInstance is ILoggerProvider instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return (ILoggerProvider)factory(serviceProvider);
        }

        return (ILoggerProvider)ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);
    }
}
