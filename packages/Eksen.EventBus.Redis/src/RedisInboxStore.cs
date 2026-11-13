using System.Text.Json;
using System.Text.Json.Serialization;
using Eksen.EventBus.Inbox;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Eksen.EventBus.Redis;

/// <summary>
/// A consumer inbox in Redis, for hosts without a database. Each (handler, event) pair is one key,
/// <c>{KeyPrefix}:{AppName}:h:{HandlerType}:{EventId}</c>, holding a small JSON record (the payload is not stored).
/// A claim is <c>SET NX</c> with <see cref="RedisInboxOptions.ProcessingTimeout"/>; completing it rewrites the key as
/// processed with <see cref="InboxOptions.IdempotencyWindow"/> as its expiry; releasing it deletes the key if the claim
/// is still the caller's. Redis expires processed records itself, so nothing is purged explicitly.
/// </summary>
public class RedisInboxStore(
    IConnectionMultiplexer connection,
    IOptions<RedisInboxOptions> redisOptions,
    IOptions<EksenEventBusOptions> eventBusOptions
) : IInboxStore
{
    private const string ProcessingStatus = "processing";
    private const string ProcessedStatus = "processed";
    private const string PendingStatus = "pending";
    private const string FailedStatus = "failed";

    private const string ReleaseScript = """
        local value = redis.call('GET', KEYS[1])
        if not value then
            return 0
        end
        local ok, record = pcall(cjson.decode, value)
        if ok and record['status'] == 'processing' and record['id'] == ARGV[1] then
            redis.call('DEL', KEYS[1])
            redis.call('DEL', KEYS[2])
            return 1
        end
        return 0
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool EnlistsInUnitOfWork
    {
        get { return false; }
    }

    private IDatabase Database
    {
        get { return connection.GetDatabase(redisOptions.Value.Database); }
    }

    private TimeSpan ProcessingTimeout
    {
        get { return redisOptions.Value.ProcessingTimeout; }
    }

    private TimeSpan IdempotencyWindow
    {
        get { return eventBusOptions.Value.Inbox.IdempotencyWindow; }
    }

    public async Task<InboxClaim> TryClaimAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        var key = GetKey(message.HandlerType, message.EventId);
        var record = RedisInboxRecord.From(message, ProcessingStatus);
        var serialized = Serialize(record);

        // A key that expires between the failed SET NX and the GET is claimable again, so try a few times.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await Database.StringSetAsync(key, serialized, ProcessingTimeout, When.NotExists))
            {
                await Database.StringSetAsync(GetMessageKey(message.Id), key.ToString(), ProcessingTimeout);
                message.Status = InboxMessageStatus.Processing;
                return new InboxClaim(InboxClaimStatus.Claimed, message);
            }

            var existing = await GetRecordAsync(key);
            if (existing == null)
            {
                continue;
            }

            return existing.Status == ProcessedStatus
                ? new InboxClaim(InboxClaimStatus.AlreadyProcessed, existing.ToMessage(message.EventId, message.HandlerType))
                : new InboxClaim(InboxClaimStatus.InProgress, existing.ToMessage(message.EventId, message.HandlerType));
        }

        return new InboxClaim(InboxClaimStatus.InProgress, null);
    }

    public async Task CompleteAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        var key = GetKey(message.HandlerType, message.EventId);
        var record = RedisInboxRecord.From(message, ProcessedStatus);
        record.ProcessedTime = DateTime.UtcNow;
        await Database.StringSetAsync(key, Serialize(record), IdempotencyWindow);
        await Database.StringSetAsync(GetMessageKey(message.Id), key.ToString(), IdempotencyWindow);
        message.Status = InboxMessageStatus.Processed;
        message.ProcessedTime = record.ProcessedTime;
    }

    public async Task ReleaseAsync(InboxMessage message, string error, CancellationToken cancellationToken = default)
    {
        var key = GetKey(message.HandlerType, message.EventId);
        await Database.ScriptEvaluateAsync(
            ReleaseScript,
            [key, GetMessageKey(message.Id)],
            [message.Id.ToString("D")]);
    }

    public async Task<bool> ExistsAsync(
        Guid eventId,
        string handlerType,
        CancellationToken cancellationToken = default)
    {
        return await Database.KeyExistsAsync(GetKey(handlerType, eventId));
    }

    public async Task<InboxMessage?> FindAsync(
        Guid eventId,
        string handlerType,
        CancellationToken cancellationToken = default)
    {
        var record = await GetRecordAsync(GetKey(handlerType, eventId));
        return record?.ToMessage(eventId, handlerType);
    }

    public async Task<bool> TryMarkAsProcessingAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var (key, record) = await GetByMessageIdAsync(messageId);
        if (record == null || record.Status == ProcessedStatus)
        {
            return false;
        }

        record.Status = ProcessingStatus;
        record.RetryCount++;
        record.LastError = null;
        await Database.StringSetAsync(key, Serialize(record), ProcessingTimeout);
        return true;
    }

    public async Task SaveAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        var key = GetKey(message.HandlerType, message.EventId);
        var status = ToStatus(message.Status);
        var expiry = status == ProcessedStatus ? IdempotencyWindow : ProcessingTimeout;
        await Database.StringSetAsync(key, Serialize(RedisInboxRecord.From(message, status)), expiry);
        await Database.StringSetAsync(GetMessageKey(message.Id), key.ToString(), expiry);
    }

    public async Task<IReadOnlyList<InboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var messages = await ScanAsync(cancellationToken);
        return messages
            .Where(message => message.Status == InboxMessageStatus.Pending)
            .OrderBy(message => message.CreationTime)
            .Take(batchSize)
            .ToList();
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var (key, record) = await GetByMessageIdAsync(messageId);
        if (record == null)
        {
            return;
        }

        record.Status = ProcessedStatus;
        record.ProcessedTime = DateTime.UtcNow;
        await Database.StringSetAsync(key, Serialize(record), IdempotencyWindow);
        await Database.StringSetAsync(GetMessageKey(messageId), key.ToString(), IdempotencyWindow);
    }

    /// <summary>
    /// A failed claim is released: the key is deleted so a redelivery can claim the event again.
    /// </summary>
    public async Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var (key, record) = await GetByMessageIdAsync(messageId);
        if (record == null || record.Status == ProcessedStatus)
        {
            return;
        }

        await Database.ScriptEvaluateAsync(ReleaseScript, [key, GetMessageKey(messageId)], [messageId.ToString("D")]);
    }

    public async Task IncrementRetryCountAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var (key, record) = await GetByMessageIdAsync(messageId);
        if (record == null)
        {
            return;
        }

        record.RetryCount++;
        await Database.StringSetAsync(key, Serialize(record), expiry: null, keepTtl: true);
    }

    /// <summary>
    /// Returns 0: Redis expires processed records after <see cref="InboxOptions.IdempotencyWindow"/> by itself.
    /// </summary>
    public Task<int> DeleteProcessedAsync(
        DateTime processedBefore,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    /// <summary>
    /// Counts by scanning this app's inbox keys; the cost grows with the number of events in the idempotency window.
    /// </summary>
    public async Task<InboxMessageStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var messages = await ScanAsync(cancellationToken);
        return new InboxMessageStats
        {
            Pending = messages.Count(message => message.Status == InboxMessageStatus.Pending),
            Processing = messages.Count(message => message.Status == InboxMessageStatus.Processing),
            Processed = messages.Count(message => message.Status == InboxMessageStatus.Processed),
            Failed = messages.Count(message => message.Status == InboxMessageStatus.Failed)
        };
    }

    public async Task<IReadOnlyList<InboxMessage>> GetMessagesAsync(
        InboxMessageStatus? status = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var messages = await ScanAsync(cancellationToken);
        return messages
            .Where(message => status == null || message.Status == status)
            .OrderByDescending(message => message.CreationTime)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    internal RedisKey GetKey(string handlerType, Guid eventId)
    {
        return $"{GetAppPrefix()}:h:{handlerType}:{eventId:N}";
    }

    private RedisKey GetMessageKey(Guid messageId)
    {
        return $"{GetAppPrefix()}:m:{messageId:N}";
    }

    private string GetAppPrefix()
    {
        return $"{redisOptions.Value.KeyPrefix}:{eventBusOptions.Value.AppName}";
    }

    private async Task<RedisInboxRecord?> GetRecordAsync(RedisKey key)
    {
        var value = await Database.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : Deserialize(value.ToString());
    }

    private async Task<(RedisKey Key, RedisInboxRecord? Record)> GetByMessageIdAsync(Guid messageId)
    {
        var key = await Database.StringGetAsync(GetMessageKey(messageId));
        if (key.IsNullOrEmpty)
        {
            return (default, null);
        }

        RedisKey recordKey = key.ToString();
        var record = await GetRecordAsync(recordKey);
        return (recordKey, record?.Id == messageId ? record : null);
    }

    private async Task<List<InboxMessage>> ScanAsync(CancellationToken cancellationToken)
    {
        var prefix = $"{GetAppPrefix()}:h:";
        var pattern = prefix + "*";
        var databaseNumber = redisOptions.Value.Database;
        var messages = new List<InboxMessage>();
        foreach (var server in connection.GetServers().Where(server => server.IsConnected && !server.IsReplica))
        {
            await foreach (var key in server.KeysAsync(databaseNumber, pattern, pageSize: 500).WithCancellation(cancellationToken))
            {
                var record = await GetRecordAsync(key);
                if (record == null || !TryParseKey(key.ToString(), prefix, out var handlerType, out var eventId))
                {
                    continue;
                }

                messages.Add(record.ToMessage(eventId, handlerType));
            }
        }

        return messages;
    }

    private static bool TryParseKey(string key, string prefix, out string handlerType, out Guid eventId)
    {
        handlerType = string.Empty;
        eventId = Guid.Empty;
        var rest = key[prefix.Length..];
        var separator = rest.LastIndexOf(':');
        if (separator <= 0 || !Guid.TryParseExact(rest[(separator + 1)..], "N", out eventId))
        {
            return false;
        }

        handlerType = rest[..separator];
        return true;
    }

    private static string ToStatus(InboxMessageStatus status)
    {
        return status switch
        {
            InboxMessageStatus.Processed => ProcessedStatus,
            InboxMessageStatus.Processing => ProcessingStatus,
            InboxMessageStatus.Failed => FailedStatus,
            _ => PendingStatus
        };
    }

    private static string Serialize(RedisInboxRecord record)
    {
        return JsonSerializer.Serialize(record, JsonOptions);
    }

    private static RedisInboxRecord? Deserialize(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<RedisInboxRecord>(value, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal sealed class RedisInboxRecord
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = PendingStatus;

        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("creationTime")]
        public DateTime CreationTime { get; set; }

        [JsonPropertyName("processedTime")]
        public DateTime? ProcessedTime { get; set; }

        [JsonPropertyName("retryCount")]
        public int RetryCount { get; set; }

        [JsonPropertyName("lastError")]
        public string? LastError { get; set; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("sourceApp")]
        public string? SourceApp { get; set; }

        public static RedisInboxRecord From(InboxMessage message, string status)
        {
            return new RedisInboxRecord
            {
                Status = status,
                Id = message.Id,
                EventType = message.EventType,
                CreationTime = message.CreationTime,
                ProcessedTime = message.ProcessedTime,
                RetryCount = message.RetryCount,
                LastError = message.LastError,
                CorrelationId = message.CorrelationId,
                SourceApp = message.SourceApp
            };
        }

        public InboxMessage ToMessage(Guid eventId, string handlerType)
        {
            return new InboxMessage
            {
                Id = Id,
                EventId = eventId,
                EventType = EventType,
                HandlerType = handlerType,
                // The payload is not kept in Redis.
                Payload = string.Empty,
                CreationTime = CreationTime,
                ProcessedTime = ProcessedTime,
                Status = Status switch
                {
                    ProcessedStatus => InboxMessageStatus.Processed,
                    ProcessingStatus => InboxMessageStatus.Processing,
                    FailedStatus => InboxMessageStatus.Failed,
                    _ => InboxMessageStatus.Pending
                },
                RetryCount = RetryCount,
                LastError = LastError,
                CorrelationId = CorrelationId,
                SourceApp = SourceApp
            };
        }
    }
}
