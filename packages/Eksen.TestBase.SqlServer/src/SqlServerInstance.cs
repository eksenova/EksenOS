using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Eksen.TestBase.SqlServer;

internal sealed class SqlServerInstance
{
    private const string SaPassword = "123*!%AbC";

    private const int EngineMemoryHeadroomMegabytes = 256;

    private const int MinimumEngineMemoryMegabytes = 512;

    private SqlServerInstance(MsSqlContainer container, IReadOnlyList<string> databaseConnectionStrings)
    {
        Container = container;
        DatabaseConnectionStrings = databaseConnectionStrings;
    }

    internal MsSqlContainer Container { get; }

    internal IReadOnlyList<string> DatabaseConnectionStrings { get; }

    internal static async Task<SqlServerInstance> CreateAsync(
        int databaseCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(databaseCount, other: 1);

        var container = BuildContainer();

        await container.StartAsync(cancellationToken);

        var adminConnectionString = container.GetConnectionString();
        await WaitUntilLoginsAreAcceptedAsync(adminConnectionString, cancellationToken);

        var databaseNames = Enumerable
            .Range(start: 0, databaseCount)
            .Select(index => $"EksenTestDb{index}")
            .ToArray();

        await CreateDatabasesAsync(adminConnectionString, databaseNames, cancellationToken);

        var connectionStrings = databaseNames
            .Select(name => new SqlConnectionStringBuilder(adminConnectionString)
            {
                InitialCatalog = name
            }.ConnectionString)
            .ToArray();

        return new SqlServerInstance(container, connectionStrings);
    }

    internal ValueTask DisposeContainerAsync()
    {
        return Container.DisposeAsync();
    }

    private static MsSqlContainer BuildContainer()
    {
        var builder = new MsSqlBuilder()
            .WithImage(SqlServerTestEnvironment.Image)
            .WithPassword(SaPassword)
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new ContainerRunningWaitStrategy()));

        if (SqlServerTestEnvironment.CpuSet is { } cpuSet && !string.IsNullOrWhiteSpace(cpuSet))
        {
            builder = builder.WithCreateParameterModifier(parameters => parameters.HostConfig.CpusetCpus = cpuSet);
        }

        if (SqlServerTestEnvironment.ContainerMemoryMegabytes is { } containerMemoryMegabytes)
        {
            var engineMemoryMegabytes = Math.Max(
                containerMemoryMegabytes - EngineMemoryHeadroomMegabytes,
                MinimumEngineMemoryMegabytes);

            builder = builder
                .WithEnvironment(name: "MSSQL_MEMORY_LIMIT_MB", engineMemoryMegabytes.ToString())
                .WithCreateParameterModifier(parameters =>
                    parameters.HostConfig.Memory = (long)containerMemoryMegabytes * 1024 * 1024);
        }

        return builder.Build();
    }

    private static async Task CreateDatabasesAsync(
        string adminConnectionString,
        IReadOnlyList<string> databaseNames,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var databaseName in databaseNames)
        {
            await using var command = new SqlCommand(
                $"""
                 CREATE DATABASE [{databaseName}];
                 ALTER DATABASE [{databaseName}] SET DELAYED_DURABILITY = FORCED;
                 """,
                connection);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task WaitUntilLoginsAreAcceptedAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(3);

        while (true)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(cmdText: "SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken);

                return;
            }
            catch (SqlException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private sealed class ContainerRunningWaitStrategy : IWaitUntil
    {
        public Task<bool> UntilAsync(IContainer container)
        {
            return Task.FromResult(true);
        }
    }
}
