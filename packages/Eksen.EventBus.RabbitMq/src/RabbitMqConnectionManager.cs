using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Eksen.EventBus.RabbitMq;

public class RabbitMqConnectionManager : IRabbitMqConnectionManager
{
    private readonly IOptions<RabbitMqEventBusOptions> _options;
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private readonly RabbitMqTelemetry _telemetry;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IConnection? _connection;
    private bool _hasConnected;

    public RabbitMqConnectionManager(
        IOptions<RabbitMqEventBusOptions> options,
        ILogger<RabbitMqConnectionManager> logger)
        : this(options, logger, meterFactory: null)
    {
    }

    public RabbitMqConnectionManager(
        IOptions<RabbitMqEventBusOptions> options,
        ILogger<RabbitMqConnectionManager> logger,
        IMeterFactory? meterFactory)
    {
        _options = options;
        _logger = logger;
        _telemetry = RabbitMqTelemetry.For(meterFactory);

        // Only a container-owned meter is disposed together with this manager's container, so the gauge callback
        // never keeps a manually created manager alive.
        if (_telemetry.IsFactoryOwned)
        {
            _telemetry.Meter.CreateObservableGauge(
                "eksen.rabbitmq.connection.up",
                () => IsConnected ? 1 : 0,
                description: "1 while the RabbitMQ connection is open, otherwise 0.");
        }
    }

    internal bool IsConnected
    {
        get { return Volatile.Read(ref _connection) is { IsOpen: true }; }
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var rabbitOptions = _options.Value;

            var factory = new ConnectionFactory
            {
                HostName = rabbitOptions.HostName,
                Port = rabbitOptions.Port,
                UserName = rabbitOptions.UserName,
                Password = rabbitOptions.Password,
                VirtualHost = rabbitOptions.VirtualHost,
                AutomaticRecoveryEnabled = rabbitOptions.AutomaticRecovery,
                NetworkRecoveryInterval = rabbitOptions.NetworkRecoveryInterval
            };

            _logger.LogInformation(
                "Connecting to RabbitMQ at {Host}:{Port}/{VHost}",
                rabbitOptions.HostName,
                rabbitOptions.Port,
                rabbitOptions.VirtualHost);

            var connection = await factory.CreateConnectionAsync(cancellationToken);
            connection.RecoverySucceededAsync += OnRecoverySucceededAsync;

            if (_hasConnected)
            {
                // The previous connection was lost and automatic recovery did not bring it back.
                RecordReconnect("new_connection");
            }

            _hasConnected = true;
            Volatile.Write(ref _connection, connection);

            _logger.LogInformation("Successfully connected to RabbitMQ");
            return connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    internal void RecordReconnect(string kind)
    {
        _telemetry.Reconnects.Add(1, new KeyValuePair<string, object?>("eksen.rabbitmq.reconnect.kind", kind));
    }

    private Task OnRecoverySucceededAsync(object sender, AsyncEventArgs args)
    {
        RecordReconnect("automatic_recovery");
        _logger.LogWarning("RabbitMQ connection recovered");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            _connection.RecoverySucceededAsync -= OnRecoverySucceededAsync;
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
