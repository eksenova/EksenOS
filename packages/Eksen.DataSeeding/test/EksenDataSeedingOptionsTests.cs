using Eksen.TestBase;
using Shouldly;

namespace Eksen.DataSeeding.Tests;

public class EksenDataSeedingOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void SeedContributors_Should_Be_Empty_By_Default()
    {
        // Arrange & Act
        var options = new EksenDataSeedingOptions();

        // Assert
        options.SeedContributors.ShouldBeEmpty();
    }

    [Fact]
    public void Add_Should_Add_Type()
    {
        // Arrange
        var options = new EksenDataSeedingOptions();

        // Act
        options.Add(typeof(StubContributorA));

        // Assert
        options.SeedContributors.ShouldContain(typeof(StubContributorA));
        options.SeedContributors.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_Should_Not_Duplicate_Same_Type()
    {
        // Arrange
        var options = new EksenDataSeedingOptions();

        // Act
        options.Add(typeof(StubContributorA));
        options.Add(typeof(StubContributorA));

        // Assert
        options.SeedContributors.Count.ShouldBe(1);
    }

    [Fact]
    public void AddRange_Should_Add_Multiple_Types()
    {
        // Arrange
        var options = new EksenDataSeedingOptions();

        // Act
        options.AddRange([typeof(StubContributorA), typeof(StubContributorB)]);

        // Assert
        options.SeedContributors.Count.ShouldBe(2);
        options.SeedContributors.ShouldContain(typeof(StubContributorA));
        options.SeedContributors.ShouldContain(typeof(StubContributorB));
    }

    [Fact]
    public void AddAssembly_Should_Discover_Contributors_From_Assembly()
    {
        // Arrange
        var options = new EksenDataSeedingOptions();

        // Act
        options.AddAssembly(typeof(StubContributorA).Assembly);

        // Assert
        options.SeedContributors.ShouldContain(typeof(StubContributorA));
        options.SeedContributors.ShouldContain(typeof(StubContributorB));
    }

    [Fact]
    public void AddAssembly_Should_Not_Include_Abstract_Types()
    {
        // Arrange
        var options = new EksenDataSeedingOptions();

        // Act
        options.AddAssembly(typeof(StubContributorA).Assembly);

        // Assert
        options.SeedContributors.ShouldNotContain(typeof(AbstractContributor));
    }

    [Fact]
    public void AddAssembly_Should_Not_Include_Interfaces()
    {
        // Arrange
        var options = new EksenDataSeedingOptions();

        // Act
        options.AddAssembly(typeof(StubContributorA).Assembly);

        // Assert
        options.SeedContributors.ShouldNotContain(typeof(IDataSeedContributor));
    }
}
