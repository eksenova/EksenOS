using System.ComponentModel.DataAnnotations;

namespace Eksen.DistributedLocks.PostgreSql;

public sealed record PostgreSqlDistributedLockOptions
{
    public const string DefaultConfigSectionPath = "Eksen:DistributedLocks:PostgreSql";

    /// <summary>
    /// The value recorded as <c>eksen.lock.name</c> when no <see cref="MetricLockNameSelector"/> is set and the
    /// selector returns nothing.
    /// </summary>
    public const string OtherMetricLockName = "other";

    private static readonly char[] MetricLockNameSeparators = [':', '/', '|'];

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Maps a lock name to the <c>eksen.lock.name</c> label of the lock metrics. Lock names usually carry an
    /// entity id (<c>order:123</c>), and every distinct label value is a new series on each histogram bucket, so
    /// the label must come from a bounded set. When unset, <see cref="DefaultMetricLockName"/> is used.
    /// </summary>
    public Func<string, string>? MetricLockNameSelector { get; set; }

    /// <summary>
    /// The lock name up to its first <c>:</c>, <c>/</c> or <c>|</c>: <c>order:123</c> and <c>order:456</c> are both
    /// recorded as <c>order</c>. A name without a separator is recorded as it is, so per-entity names must use one
    /// of these separators or set <see cref="MetricLockNameSelector"/>.
    /// </summary>
    public static string DefaultMetricLockName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var separator = name.IndexOfAny(MetricLockNameSeparators);
        var prefix = separator < 0 ? name : name[..separator];
        return string.IsNullOrWhiteSpace(prefix) ? OtherMetricLockName : prefix;
    }

    internal string ResolveMetricLockName(string name)
    {
        var selected = (MetricLockNameSelector ?? DefaultMetricLockName)(name);
        return string.IsNullOrWhiteSpace(selected) ? OtherMetricLockName : selected;
    }
}
