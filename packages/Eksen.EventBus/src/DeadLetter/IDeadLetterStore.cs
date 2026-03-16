namespace Eksen.EventBus.DeadLetter;

public interface IDeadLetterStore
{
    Task SaveAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeadLetterMessage>> GetMessagesAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<DeadLetterMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task RequeueAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
