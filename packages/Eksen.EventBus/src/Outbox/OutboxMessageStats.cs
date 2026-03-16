namespace Eksen.EventBus.Outbox;

public class OutboxMessageStats
{
    public int Pending { get; set; }

    public int Processing { get; set; }

    public int Processed { get; set; }

    public int Failed { get; set; }
}
