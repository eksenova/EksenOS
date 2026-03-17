using Eksen.ErrorHandling;
using Eksen.SmartEnums;
using Eksen.SmartEnums.Tests.Fakes;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.SmartEnums.Tests;

public class EnumerationTests : EksenUnitTestBase
{
    [Fact]
    public void Code_Should_Return_Member_Name()
    {
        // Arrange & Act & Assert
        TestColor.Red.Code.ShouldBe("Red");
        TestColor.Green.Code.ShouldBe("Green");
        TestColor.Blue.Code.ShouldBe("Blue");
    }

    [Fact]
    public void GetValues_Should_Return_All_Members()
    {
        // Arrange & Act
        var values = TestColor.GetValues();

        // Assert
        values.Count.ShouldBe(3);
        values.ShouldContain(TestColor.Red);
        values.ShouldContain(TestColor.Green);
        values.ShouldContain(TestColor.Blue);
    }

    [Fact]
    public void MaxLength_Should_Return_Longest_Code_Length()
    {
        // Arrange & Act
        var maxLength = TestSize.MaxLength;

        // Assert
        maxLength.ShouldBe("ExtraLarge".Length);
    }

    [Fact]
    public void Parse_Should_Return_Matching_Member()
    {
        // Arrange & Act
        var result = TestColor.Parse("Red");

        // Assert
        result.ShouldBe(TestColor.Red);
    }

    [Fact]
    public void Parse_Should_Be_Case_Insensitive()
    {
        // Arrange & Act
        var result = TestColor.Parse("red");

        // Assert
        result.ShouldBe(TestColor.Red);
    }

    [Fact]
    public void Parse_Should_Ignore_Spaces()
    {
        // Arrange & Act
        var result = TestSize.Parse("Extra Large");

        // Assert
        result.ShouldBe(TestSize.ExtraLarge);
    }

    [Fact]
    public void Parse_Should_Throw_When_Code_Not_Found()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => TestColor.Parse("Yellow"));
        exception.Descriptor.ErrorType.ShouldContain("NotFound");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Should_Throw_When_Null_Or_Empty(string? code)
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => TestColor.Parse(code!));
    }

    [Fact]
    public void ToString_Should_Contain_Code()
    {
        // Arrange & Act & Assert
        TestColor.Green.ToString().ShouldContain("Green");
    }

    [Fact]
    public void CompareTo_Should_Compare_By_Code_Ordinal()
    {
        // Arrange & Act & Assert
        TestColor.Blue.CompareTo(TestColor.Red).ShouldBeLessThan(0);
        TestColor.Red.CompareTo(TestColor.Blue).ShouldBeGreaterThan(0);
        TestColor.Red.CompareTo(TestColor.Red).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_Null_Should_Return_Positive()
    {
        // Arrange & Act & Assert
        TestColor.Red.CompareTo((TestColor?)null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetHashCode_Should_Be_Consistent()
    {
        // Arrange & Act & Assert
        TestColor.Red.GetHashCode().ShouldBe(TestColor.Red.GetHashCode());
        TestColor.Red.GetHashCode().ShouldNotBe(TestColor.Blue.GetHashCode());
    }

    [Fact]
    public void Equality_Should_Work_By_Reference_Identity()
    {
        // Arrange & Act & Assert
        TestColor.Red.ShouldBe(TestColor.Red);
        TestColor.Red.ShouldNotBe(TestColor.Blue);
    }
}
