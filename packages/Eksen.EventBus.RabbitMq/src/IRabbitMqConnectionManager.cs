using RabbitMQ.Client;

namespace Eksen.EventBus.RabbitMq;

public interface IRabbitMqConnectionManager : IAsyncDisposable
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
