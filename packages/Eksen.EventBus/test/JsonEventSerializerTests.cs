using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Finance;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class JsonEventSerializerTests : EksenUnitTestBase
{
    private readonly JsonEventSerializer _serializer = new();

    private sealed class ValueObjectEvent : IntegrationEvent
    {
        public required EmailAddress CustomerEmail { get; init; }

        public required MoneyAmount Amount { get; init; }

        public required Currency Currency { get; init; }
    }

    [Fact]
    public void Serialize_Should_Render_Value_Objects_And_Smart_Enums_As_Bare_Primitives()
    {
        // Arrange
        var @event = new ValueObjectEvent
        {
            CustomerEmail = EmailAddress.Create("buyer@example.com"),
            Amount = MoneyAmount.Create(49.90m),
            Currency = Currency.Usd
        };

        // Act
        var json = _serializer.Serialize(@event);

        // Assert — value objects and smart enums are flat, not wrapped objects
        json.ShouldContain("\"customerEmail\":\"buyer@example.com\"");
        json.ShouldContain("\"currency\":\"Usd\"");
        json.ShouldNotContain("\"value\"", Case.Insensitive);
    }

    [Fact]
    public void Roundtrip_Should_Preserve_Value_Objects_And_Smart_Enums()
    {
        // Arrange
        var original = new ValueObjectEvent
        {
            CustomerEmail = EmailAddress.Create("buyer@example.com"),
            Amount = MoneyAmount.Create(49.90m),
            Currency = Currency.Usd
        };
        var json = _serializer.Serialize(original);

        // Act
        var deserialized = _serializer.Deserialize<ValueObjectEvent>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.CustomerEmail.ShouldBe(original.CustomerEmail);
        deserialized.Amount.ShouldBe(original.Amount);
        deserialized.Currency.ShouldBe(Currency.Usd);
    }

    [Fact]
    public void Serialize_Should_Return_Json_String()
    {
        // Arrange
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 99.99m };

        // Act
        var json = _serializer.Serialize(@event);

        // Assert
        json.ShouldNotBeNullOrWhiteSpace();
        json.ShouldContain("orderNumber");
        json.ShouldContain("ORD-1");
    }

    [Fact]
    public void Serialize_Should_Use_CamelCase()
    {
        // Arrange
        var @event = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };

        // Act
        var json = _serializer.Serialize(@event);

        // Assert
        json.ShouldContain("\"orderNumber\"");
        json.ShouldContain("\"amount\"");
        json.ShouldNotContain("\"OrderNumber\"", Case.Sensitive);
    }

    [Fact]
    public void Deserialize_Generic_Should_Return_Event()
    {
        // Arrange
        var original = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 42.5m };
        var json = _serializer.Serialize(original);

        // Act
        var deserialized = _serializer.Deserialize<TestOrderCreatedEvent>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.OrderNumber.ShouldBe("ORD-1");
        deserialized.Amount.ShouldBe(42.5m);
    }

    [Fact]
    public void Deserialize_Type_Should_Return_Event()
    {
        // Arrange
        var original = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 42.5m };
        var json = _serializer.Serialize(original);

        // Act
        var deserialized = _serializer.Deserialize(json, typeof(TestOrderCreatedEvent));

        // Assert
        deserialized.ShouldNotBeNull();
        var typed = deserialized.ShouldBeOfType<TestOrderCreatedEvent>();
        typed.OrderNumber.ShouldBe("ORD-1");
        typed.Amount.ShouldBe(42.5m);
    }

    [Fact]
    public void Roundtrip_Should_Preserve_EventId()
    {
        // Arrange
        var original = new TestOrderCreatedEvent { OrderNumber = "ORD-1", Amount = 100m };
        var json = _serializer.Serialize(original);

        // Act
        var deserialized = _serializer.Deserialize<TestOrderCreatedEvent>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.EventId.ShouldBe(original.EventId);
    }

    [Fact]
    public void Roundtrip_Should_Preserve_CorrelationId()
    {
        // Arrange
        var original = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            CorrelationId = "corr-xyz"
        };
        var json = _serializer.Serialize(original);

        // Act
        var deserialized = _serializer.Deserialize<TestOrderCreatedEvent>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.CorrelationId.ShouldBe("corr-xyz");
    }

    [Fact]
    public void Roundtrip_Should_Preserve_SourceApp()
    {
        // Arrange
        var original = new TestOrderCreatedEvent
        {
            OrderNumber = "ORD-1",
            Amount = 100m,
            SourceApp = "OrderService"
        };
        var json = _serializer.Serialize(original);

        // Act
        var deserialized = _serializer.Deserialize<TestOrderCreatedEvent>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.SourceApp.ShouldBe("OrderService");
    }
}
