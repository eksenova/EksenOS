namespace Eksen.EventBus.Inbox;

public class InboxMessageStats
{
    public int Pending { get; set; }

    public int Processing { get; set; }

    public int Processed { get; set; }

    public int Failed { get; set; }
}
