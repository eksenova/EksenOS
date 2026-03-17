namespace Eksen.EventBus.Tests;

public class TestOrderCreatedEvent : IntegrationEvent
{
    public string OrderNumber { get; init; } = null!;

    public decimal Amount { get; init; }
}

public class TestPaymentProcessedEvent : IntegrationEvent
{
    public string PaymentId { get; init; } = null!;
}
