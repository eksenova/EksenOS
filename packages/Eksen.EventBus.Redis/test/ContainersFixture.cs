using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using RabbitMQ.Client;
using Testcontainers.Redis;

namespace Eksen.EventBus.Redis.Tests;

/// <summary>
/// Disposable Redis and RabbitMQ brokers (Docker) shared by the tests of one class.
/// </summary>
public sealed class ContainersFixture : IAsyncLifetime
{
    private RedisContainer? _redis;
    private IContainer? _rabbit;

    public string RedisConfiguration { get; private set; } = "";

    public string RabbitHostName { get; private set; } = "localhost";

    public int RabbitPort { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var dockerSocket = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker/run/docker.sock");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")) && File.Exists(dockerSocket))
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "unix://" + dockerSocket);
        }

        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");

        _redis = new RedisBuilder("redis:7-alpine").Build();
        _rabbit = new ContainerBuilder("rabbitmq:3-alpine")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5672))
            .Build();
        await Task.WhenAll(_redis.StartAsync(), _rabbit.StartAsync());

        RedisConfiguration = _redis.GetConnectionString();
        RabbitHostName = _rabbit.Hostname;
        RabbitPort = _rabbit.GetMappedPublicPort(5672);

        var factory = new ConnectionFactory { HostName = RabbitHostName, Port = RabbitPort };
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
        if (_redis != null)
        {
            await _redis.DisposeAsync();
        }

        if (_rabbit != null)
        {
            await _rabbit.DisposeAsync();
        }
    }
}
