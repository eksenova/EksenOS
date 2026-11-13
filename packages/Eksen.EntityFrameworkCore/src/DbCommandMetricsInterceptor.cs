using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Eksen.EntityFrameworkCore;

public sealed class EksenDbCommandMetricsOptions
{
    /// <summary>
    /// Overrides the <c>db.system.name</c> tag. When empty it is derived from the EF Core provider:
    /// Npgsql gives <c>postgresql</c>, SQL Server gives <c>mssql</c> and SQLite gives <c>sqlite</c>.
    /// </summary>
    public string? DbSystemName { get; set; }
}

/// <summary>
/// Records <c>eksen.db.command.duration</c> for every command EF Core executes, tagged with
/// <c>db.system.name</c>, <c>db.operation.name</c>, <c>db.namespace</c>, <c>server.address</c> and, on failure,
/// <c>error.type</c>. The name avoids a collision with the <c>db.client.operation.duration</c> instrument of the
/// database drivers' own meters.
/// </summary>
public sealed class DbCommandMetricsInterceptor : DbCommandInterceptor
{
    private static readonly ConditionalWeakTable<DbConnection, ConnectionAttributes> ConnectionAttributeCache = new();

    private readonly EntityFrameworkCoreTelemetry _telemetry;
    private readonly string? _configuredSystemName;

    public DbCommandMetricsInterceptor(
        IMeterFactory? meterFactory = null,
        IOptions<EksenDbCommandMetricsOptions>? options = null)
    {
        _telemetry = EntityFrameworkCoreTelemetry.For(meterFactory);
        _configuredSystemName = options?.Value.DbSystemName;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Record(command, eventData, error: null);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData, error: null);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Record(command, eventData, error: null);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData, error: null);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Record(command, eventData, error: null);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData, error: null);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Record(command, eventData, eventData.Exception);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData, eventData.Exception);
        return Task.CompletedTask;
    }

    private void Record(DbCommand command, CommandEndEventData eventData, Exception? error)
    {
        try
        {
            var connectionAttributes = ResolveConnectionAttributes(command.Connection);

            var tags = new TagList
            {
                { EksenSemanticConventions.DbSystemName, ResolveSystemName(eventData, command.Connection) },
                { EksenSemanticConventions.DbOperationName, ClassifyOperation(command.CommandText) }
            };

            if (connectionAttributes.Namespace is { Length: > 0 })
            {
                tags.Add(EksenSemanticConventions.DbNamespace, connectionAttributes.Namespace);
            }

            if (connectionAttributes.ServerAddress is { Length: > 0 })
            {
                tags.Add(EksenSemanticConventions.ServerAddress, connectionAttributes.ServerAddress);
            }

            if (error is not null)
            {
                tags.Add(EksenSemanticConventions.ErrorType, error.GetType().FullName ?? error.GetType().Name);
            }

            _telemetry.CommandDuration.Record(eventData.Duration.TotalSeconds, tags);
        }
        catch
        {
            // Telemetry must never break a database command.
        }
    }

    private string ResolveSystemName(CommandEndEventData eventData, DbConnection? connection)
    {
        if (!string.IsNullOrWhiteSpace(_configuredSystemName))
        {
            return _configuredSystemName;
        }

        string? providerName = null;
        try
        {
            providerName = eventData.Context?.Database.ProviderName;
        }
        catch
        {
            // A disposed context cannot report its provider; fall back to the connection type.
        }

        return MapSystemName(providerName ?? connection?.GetType().FullName);
    }

    internal static string MapSystemName(string? providerOrConnectionType)
    {
        if (string.IsNullOrEmpty(providerOrConnectionType))
        {
            return "other_sql";
        }

        if (providerOrConnectionType.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
            || providerOrConnectionType.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            return "postgresql";
        }

        if (providerOrConnectionType.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
            || providerOrConnectionType.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            return "mssql";
        }

        if (providerOrConnectionType.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return "sqlite";
        }

        if (providerOrConnectionType.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            return "mysql";
        }

        if (providerOrConnectionType.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            return "in_memory";
        }

        return "other_sql";
    }

    private static ConnectionAttributes ResolveConnectionAttributes(DbConnection? connection)
    {
        return connection is null
            ? ConnectionAttributes.Empty
            : ConnectionAttributeCache.GetValue(connection, CreateConnectionAttributes);
    }

    private static ConnectionAttributes CreateConnectionAttributes(DbConnection connection)
    {
        string? databaseName = null;
        string? serverAddress = null;

        try
        {
            databaseName = connection.Database;
        }
        catch
        {
            // The connection may be in a transient state.
        }

        try
        {
            serverAddress = ExtractServerAddress(connection.DataSource);
        }
        catch
        {
            // Same as above.
        }

        return new ConnectionAttributes(databaseName, serverAddress);
    }

    /// <summary>
    /// Extracts the host from a provider data source: <c>tcp://host:5432</c> (Npgsql), <c>tcp:host,1433</c> or
    /// <c>host\instance</c> (SQL Server). A SQLite file, <c>:memory:</c> or a Unix socket yields no address.
    /// </summary>
    internal static string? ExtractServerAddress(string? dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return null;
        }

        var value = dataSource.Trim();
        var schemeSeparator = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator >= 0)
        {
            value = value[(schemeSeparator + 3)..];
        }
        else
        {
            if (value.StartsWith(':')
                || value.StartsWith("unix:", StringComparison.OrdinalIgnoreCase)
                || value.Contains('/')
                || (value.Length > 2 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] == '\\')
                || value.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            foreach (var protocol in SqlServerProtocols)
            {
                if (value.StartsWith(protocol, StringComparison.OrdinalIgnoreCase))
                {
                    value = value[protocol.Length..];
                    break;
                }
            }
        }

        if (value.StartsWith('['))
        {
            var closing = value.IndexOf(']');
            return closing > 1 ? value[1..closing] : null;
        }

        var end = value.IndexOfAny([',', '\\', ':', '/', ';']);
        var host = (end >= 0 ? value[..end] : value).Trim();
        return host.Length == 0 ? null : host;
    }

    private static readonly string[] SqlServerProtocols = ["tcp:", "np:", "lpc:", "admin:"];

    /// <summary>
    /// The first SQL keyword of the command, upper-cased (<c>SELECT</c>, <c>INSERT</c>, <c>WITH</c>, ...), or
    /// <c>other</c>. Leading comments and parentheses are skipped.
    /// </summary>
    internal static string ClassifyOperation(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return "other";
        }

        var span = commandText.AsSpan();
        while (true)
        {
            span = span.TrimStart();
            if (span.StartsWith("--"))
            {
                var newline = span.IndexOf('\n');
                if (newline < 0)
                {
                    return "other";
                }

                span = span[(newline + 1)..];
                continue;
            }

            if (span.StartsWith("/*"))
            {
                var close = span.IndexOf("*/");
                if (close < 0)
                {
                    return "other";
                }

                span = span[(close + 2)..];
                continue;
            }

            if (span.StartsWith("("))
            {
                span = span[1..];
                continue;
            }

            break;
        }

        var length = 0;
        while (length < span.Length && char.IsAsciiLetter(span[length]))
        {
            length++;
        }

        return length == 0 ? "other" : span[..length].ToString().ToUpperInvariant();
    }

    private sealed record ConnectionAttributes(string? Namespace, string? ServerAddress)
    {
        public static readonly ConnectionAttributes Empty = new(Namespace: null, ServerAddress: null);
    }
}
