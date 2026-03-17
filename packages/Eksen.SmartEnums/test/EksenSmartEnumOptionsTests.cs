using System.Reflection;
using Eksen.SmartEnums;
using Eksen.SmartEnums.Tests.Fakes;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.SmartEnums.Tests;

public class EksenSmartEnumOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Add_Generic_Should_Register_Enumeration_Type()
    {
        // Arrange
        var options = new EksenSmartEnumOptions();

        // Act
        options.Add<TestColor>();

        // Assert
        options.EnumerationTypes.ShouldContain(typeof(TestColor));
    }

    [Fact]
    public void Add_Generic_Should_Not_Duplicate()
    {
        // Arrange
        var options = new EksenSmartEnumOptions();

        // Act
        options.Add<TestColor>();
        options.Add<TestColor>();

        // Assert
        options.EnumerationTypes.Count(t => t == typeof(TestColor)).ShouldBe(1);
    }

    [Fact]
    public void Add_ByType_Should_Register_Enumeration_Type()
    {
        // Arrange
        var options = new EksenSmartEnumOptions();

        // Act
        options.Add(typeof(TestColor));

        // Assert
        options.EnumerationTypes.ShouldContain(typeof(TestColor));
    }

    [Fact]
    public void Add_Should_Throw_When_Type_Is_Not_Enumeration()
    {
        // Arrange
        var options = new EksenSmartEnumOptions();

        // Act & Assert
        Should.Throw<Exception>(() => options.Add(typeof(string)));
    }

    [Fact]
    public void AddRange_Should_Register_Multiple_Types()
    {
        // Arrange
        var options = new EksenSmartEnumOptions();

        // Act
        options.AddRange([typeof(TestColor), typeof(TestSize)]);

        // Assert
        options.EnumerationTypes.Count.ShouldBe(2);
    }

    [Fact]
    public void AddAssembly_Should_Register_All_Enumerations_In_Assembly()
    {
        // Arrange
        var options = new EksenSmartEnumOptions();

        // Act
        options.AddAssembly(typeof(TestColor).Assembly);

        // Assert
        options.EnumerationTypes.ShouldContain(typeof(TestColor));
        options.EnumerationTypes.ShouldContain(typeof(TestSize));
    }

    [Fact]
    public void EnumerationTypes_Should_Be_Empty_InIitially()
    {
        // Arrange & Act
        var options = new EksenSmartEnumOptions();

        // Assert
        options.EnumerationTypes.ShouldBeEmpty();
    }
}
