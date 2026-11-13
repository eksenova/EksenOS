namespace Eksen.EventBus.Redis;

public class RedisInboxOptions
{
    /// <summary>
    /// A StackExchange.Redis configuration string. When null, the <c>IConnectionMultiplexer</c> registered in the
    /// container is used.
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// Prefix of every inbox key. Keys are <c>{KeyPrefix}:{AppName}:h:{HandlerType}:{EventId}</c>.
    /// </summary>
    public string KeyPrefix { get; set; } = "eksen:inbox";

    /// <summary>
    /// How long a claim stays valid while its handler runs. A consumer that dies mid-handler loses its claim after this
    /// long and a redelivery can claim the event again; keep it above the longest handler run including its retries.
    /// </summary>
    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The Redis database number; -1 is the connection's default database.
    /// </summary>
    public int Database { get; set; } = -1;
}
