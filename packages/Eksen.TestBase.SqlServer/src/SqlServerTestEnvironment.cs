using System.Runtime.InteropServices;

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
    /// Environment variable overriding the container image used for the SQL Server worker.
    /// </summary>
    public const string ImageVariable = "EKSEN_TEST_SQLSERVER_IMAGE";

    /// <summary>
    /// The amd64 SQL Server image used by default. It is the production-parity engine and is what CI runs.
    /// </summary>
    public const string DefaultSqlServerImage = "mcr.microsoft.com/mssql/server:2025-latest";

    /// <summary>
    /// The arm64-native default image. The SQL Server images ship for amd64 only and segfault under
    /// emulation on arm64 hosts (Apple Silicon), so Azure SQL Edge — the SQL Server engine built for
    /// arm64 — is used there instead.
    /// </summary>
    public const string DefaultArm64Image = "mcr.microsoft.com/azure-sql-edge:latest";

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

    /// <summary>
    /// The container image for the SQL Server worker. An explicit <see cref="ImageVariable"/> wins;
    /// otherwise the default is chosen by host architecture so arm64 machines get an engine that runs
    /// natively (<see cref="DefaultArm64Image"/>) while everything else keeps production parity
    /// (<see cref="DefaultSqlServerImage"/>).
    /// </summary>
    public static string Image
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(ImageVariable);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? DefaultArm64Image
                : DefaultSqlServerImage;
        }
    }
}
