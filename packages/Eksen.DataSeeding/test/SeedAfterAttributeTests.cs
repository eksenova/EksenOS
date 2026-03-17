using Eksen.TestBase;
using Shouldly;

namespace Eksen.DataSeeding.Tests;

public class SeedAfterAttributeTests : EksenUnitTestBase
{
    [Fact]
    public void Type_Should_Be_Set_From_Constructor()
    {
        // Arrange & Act
        var attribute = new SeedAfterAttribute(typeof(StubContributorA));

        // Assert
        attribute.Type.ShouldBe(typeof(StubContributorA));
    }

    [Fact]
    public void Should_Allow_Multiple_On_Same_Class()
    {
        // Arrange & Act
        var attributes = typeof(StubContributorAfterAAndB)
            .GetCustomAttributes(typeof(SeedAfterAttribute), false)
            .Cast<SeedAfterAttribute>()
            .ToList();

        // Assert
        attributes.Count.ShouldBe(2);
        attributes.ShouldContain(a => a.Type == typeof(StubContributorA));
        attributes.ShouldContain(a => a.Type == typeof(StubContributorB));
    }
}
