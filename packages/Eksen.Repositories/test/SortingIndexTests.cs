using Eksen.ErrorHandling;
using Eksen.Repositories;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Repositories.Tests;

public class SortingIndexTests : EksenUnitTestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Create_Should_Succeed_With_Valid_Value(int value)
    {
        // Arrange & Act
        var index = SortingIndex.Create(value);

        // Assert
        index.Value.ShouldBe(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Create_Should_Throw_When_Negative(int value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => SortingIndex.Create(value));
        exception.Descriptor.ShouldBe(RepositoriesErrors.NegativeSortingIndex);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("42", 42)]
    public void Parse_Should_Succeed_With_Valid_String(string input, int expected)
    {
        // Arrange & Act
        var index = SortingIndex.Parse(input);

        // Assert
        index.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.5")]
    public void Parse_Should_Throw_When_Invalid_String(string input)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => SortingIndex.Parse(input));
        exception.Descriptor.ShouldBe(RepositoriesErrors.InvalidSortingIndex);
    }

    [Fact]
    public void ToParseableString_Should_Return_String_Representation()
    {
        // Arrange
        var index = SortingIndex.Create(42);

        // Act
        var result = index.ToParseableString();

        // Assert
        result.ShouldBe("42");
    }

    [Fact]
    public void CompareTo_Should_Order_Correctly()
    {
        // Arrange
        var a = SortingIndex.Create(1);
        var b = SortingIndex.Create(5);
        var c = SortingIndex.Create(1);

        // Act & Assert
        a.CompareTo(b).ShouldBeLessThan(0);
        b.CompareTo(a).ShouldBeGreaterThan(0);
        a.CompareTo(c).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_Should_Handle_Null()
    {
        // Arrange
        var index = SortingIndex.Create(0);

        // Act
        var result = index.CompareTo(null);

        // Assert
        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Equality_Should_Work_By_Value()
    {
        // Arrange
        var a = SortingIndex.Create(3);
        var b = SortingIndex.Create(3);
        var c = SortingIndex.Create(4);

        // Act & Assert
        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }
}
