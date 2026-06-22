namespace Eksen.TestBase.SqlServer;

/// <summary>
/// Resolves the environment-driven configuration that governs the shared SQL Server worker pool.
/// </summary>
public static class SqlServerTestEnvironment
{
    /// <summary>
    /// Environment variable controlling how many SQL Server containers the assembly-wide pool may run.
    /// </summary>
    public const string MaxWorkersVariable = "EKSEN_SQL_MAX_WORKERS";

    /// <summary>
    /// Environment variable controlling the logical CPU set pinned on every SQL Server container.
    /// </summary>
    public const string CpuSetVariable = "EKSEN_SQL_CPUSET";

    /// <summary>
    /// Upper bound on the number of concurrent SQL Server containers, regardless of the requested value.
    /// </summary>
    public const int MaxWorkersHardCap = 10;

    /// <summary>
    /// Default CPU set. SQL Server 2025 asserts on an odd logical-CPU count, so the default pins an even set.
    /// </summary>
    public const string DefaultCpuSet = "0-3";

    /// <summary>
    /// The pool size: the requested worker count clamped to the inclusive range [1, <see cref="MaxWorkersHardCap"/>].
    /// </summary>
    public static int MaxWorkers
    {
        get
        {
            var requested =
                int.TryParse(Environment.GetEnvironmentVariable(MaxWorkersVariable), out var value) && value > 0
                    ? value
                    : MaxWorkersHardCap;

            return Math.Min(requested, MaxWorkersHardCap);
        }
    }

    /// <summary>
    /// The logical CPU set pinned on each container. Falls back to <see cref="DefaultCpuSet"/> when unset.
    /// </summary>
    public static string CpuSet
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(CpuSetVariable);
            return string.IsNullOrWhiteSpace(configured) ? DefaultCpuSet : configured;
        }
    }
}
