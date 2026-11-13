namespace Eksen.EventBus;

/// <summary>
/// A message that can never be handled: its payload does not deserialize, its type is unknown or no handler is
/// registered for it. Poison messages skip every retry and go straight to the dead-letter destination.
/// </summary>
public class PoisonMessageException : Exception
{
    public PoisonMessageException(string message)
        : base(message)
    {
    }

    public PoisonMessageException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
