using Eksen.SmartEnums;
using Eksen.SmartEnums.Tests.Fakes;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.SmartEnums.Tests;

public class SmartEnumTypeExtensionsTests : EksenUnitTestBase
{
    [Fact]
    public void IsEnumeration_Should_Return_True_For_Enumeration_Type()
    {
        // Arrange & Act & Assert
        typeof(TestColor).IsEnumeration.ShouldBeTrue();
    }

    [Fact]
    public void IsEnumeration_Should_Return_False_For_Non_Enumeration_Class()
    {
        // Arrange & Act & Assert
        typeof(string).IsEnumeration.ShouldBeFalse();
    }

    [Fact]
    public void IsEnumeration_Should_Return_False_For_Abstract_Enumeration()
    {
        // Arrange & Act & Assert
        typeof(Enumeration<TestColor>).IsEnumeration.ShouldBeFalse();
    }

    [Fact]
    public void IsEnumeration_Should_Return_False_For_Value_Types()
    {
        // Arrange & Act & Assert
        typeof(int).IsEnumeration.ShouldBeFalse();
    }

    [Fact]
    public void IsEnumeration_Should_Return_False_For_Interface()
    {
        // Arrange & Act & Assert
        typeof(IEnumeration).IsEnumeration.ShouldBeFalse();
    }
}
