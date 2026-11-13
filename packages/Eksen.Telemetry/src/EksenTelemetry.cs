namespace Eksen.Telemetry;

/// <summary>
/// Names of every <see cref="System.Diagnostics.ActivitySource"/> and
/// <see cref="System.Diagnostics.Metrics.Meter"/> owned by an Eksen package. A source and the meter of
/// the same concern share one name.
/// </summary>
public static class EksenTelemetry
{
    public const string SourcePrefix = "Eksen";
    public const string SourceWildcard = SourcePrefix + ".*";
    public const string EventBus = "Eksen.EventBus";
    public const string EventBusRabbitMq = "Eksen.EventBus.RabbitMq";
    public const string UnitOfWork = "Eksen.UnitOfWork";
    public const string EntityFrameworkCore = "Eksen.EntityFrameworkCore";
    public const string ErrorHandling = "Eksen.ErrorHandling";
    public const string Permissions = "Eksen.Permissions";
    public const string Emailing = "Eksen.Emailing";
    public const string HttpOutbound = "Eksen.Http.Outbound";
    public const string Background = "Eksen.Background";
    public const string Auditing = "Eksen.Auditing";
    public const string DistributedLocks = "Eksen.DistributedLocks";
    public const string RequestLogCategory = "Eksen.Telemetry.RequestLog";

    public static IReadOnlyList<string> AllNames { get; } =
    [
        EventBus,
        EventBusRabbitMq,
        UnitOfWork,
        EntityFrameworkCore,
        ErrorHandling,
        Permissions,
        Emailing,
        HttpOutbound,
        Background,
        Auditing,
        DistributedLocks
    ];
}
