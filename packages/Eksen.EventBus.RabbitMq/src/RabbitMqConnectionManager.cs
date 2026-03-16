using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Eksen.EventBus.RabbitMq;

public class RabbitMqConnectionManager(
    IOptions<RabbitMqEventBusOptions> options,
    ILogger<RabbitMqConnectionManager> logger) : IRabbitMqConnectionManager
{
    private IConnection? _connection;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var rabbitOptions = options.Value;

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

            logger.LogInformation(
                "Connecting to RabbitMQ at {Host}:{Port}/{VHost}",
                rabbitOptions.HostName,
                rabbitOptions.Port,
                rabbitOptions.VirtualHost);

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            logger.LogInformation("Successfully connected to RabbitMQ");
            return _connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
