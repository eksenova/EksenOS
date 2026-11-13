namespace Eksen.Telemetry.OpenTelemetry.Tests;

/// <summary>
/// Tests that build an OpenTelemetry tracer provider. Activity listeners are process-wide, so one test's provider
/// samples the activities another test starts; these tests run one at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalTelemetryCollection
{
    public const string Name = "Global telemetry listeners";
}
