using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using RabbitMQ.Client;

namespace Eksen.EventBus.RabbitMq.Tests;

/// <summary>
/// A disposable RabbitMQ broker (Docker) shared by the broker tests of one class.
/// </summary>
public sealed class RabbitMqBrokerFixture : IAsyncLifetime
{
    private IContainer? _container;

    public string HostName { get; private set; } = "localhost";

    public int Port { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var dockerSocket = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker/run/docker.sock");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")) && File.Exists(dockerSocket))
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "unix://" + dockerSocket);
        }

        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");

        _container = new ContainerBuilder("rabbitmq:3-alpine")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .Build();
        await _container.StartAsync();
        HostName = _container.Hostname;
        Port = _container.GetMappedPublicPort(5672);

        // The port opens before the broker accepts AMQP connections.
        var factory = new ConnectionFactory { HostName = HostName, Port = Port };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync();
                break;
            }
            catch (Exception) when (attempt < 60)
            {
                await Task.Delay(500);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
