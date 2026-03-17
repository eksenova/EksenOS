using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class EventNameResolverTests : EksenUnitTestBase
{
    [Fact]
    public void GetEventName_Generic_Should_Return_FullName()
    {
        // Arrange & Act
        var name = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Assert
        name.ShouldBe(typeof(TestOrderCreatedEvent).FullName);
    }

    [Fact]
    public void GetEventName_Type_Should_Return_FullName()
    {
        // Arrange & Act
        var name = EventNameResolver.GetEventName(typeof(TestPaymentProcessedEvent));

        // Assert
        name.ShouldBe(typeof(TestPaymentProcessedEvent).FullName);
    }

    [Fact]
    public void GetEventName_Should_Cache_Results()
    {
        // Arrange & Act
        var name1 = EventNameResolver.GetEventName<TestOrderCreatedEvent>();
        var name2 = EventNameResolver.GetEventName<TestOrderCreatedEvent>();

        // Assert
        name1.ShouldBe(name2);
    }

    [Fact]
    public void GetEventName_Should_Return_Same_Result_For_Generic_And_Type_Overloads()
    {
        // Arrange & Act
        var genericName = EventNameResolver.GetEventName<TestOrderCreatedEvent>();
        var typeName = EventNameResolver.GetEventName(typeof(TestOrderCreatedEvent));

        // Assert
        genericName.ShouldBe(typeName);
    }
}
