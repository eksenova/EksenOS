using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;

namespace Eksen.Telemetry.OpenTelemetry;

/// <summary>
/// The resource attributes every signal carries.
/// </summary>
public static class EksenTelemetryResource
{
    public static readonly string ServiceInstanceId = Guid.NewGuid().ToString("N");

    public static string ResolveServiceVersion()
    {
        var assembly = Assembly.GetEntryAssembly();

        return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly?.GetName().Version?.ToString()
               ?? "0.0.0";
    }

    public static string ResolveDeploymentVersion(Func<string, string?>? getEnvironmentVariable = null)
    {
        var value = (getEnvironmentVariable ?? Environment.GetEnvironmentVariable)("DEPLOYMENT_VERSION");
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
    }

    /// <summary>
    /// Attributes added next to <c>service.name</c>, <c>service.namespace</c>, <c>service.version</c> and
    /// <c>service.instance.id</c>.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, object>> BuildAttributes(
        EksenTelemetryOptions options,
        IHostEnvironment environment,
        Func<string, string?>? getEnvironmentVariable = null)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            new("deployment.environment", environment.EnvironmentName.ToLowerInvariant()),
            new("deployment.version", ResolveDeploymentVersion(getEnvironmentVariable)),
            new("host.name", Environment.MachineName),
            new("process.runtime.name", ".NET"),
            new("process.runtime.version", Environment.Version.ToString()),
            new("process.runtime.description", RuntimeInformation.FrameworkDescription),
            new("os.type", ResolveOsType())
        };

        if (!string.IsNullOrWhiteSpace(options.ServiceRole))
        {
            attributes.Add(new KeyValuePair<string, object>("service.role", options.ServiceRole));
        }

        return attributes;
    }

    private static string ResolveOsType()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "darwin";
        }

        if (OperatingSystem.IsFreeBSD())
        {
            return "freebsd";
        }

        return RuntimeInformation.OSDescription;
    }
}
