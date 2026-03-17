using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Identification;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class FirstNameTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var name = FirstName.Create("John");

        // Assert
        name.Value.ShouldBe("John");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var name = FirstName.Create("  John  ");

        // Assert
        name.Value.ShouldBe("John");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => FirstName.Create(value!));
        exception.Descriptor.ShouldBe(IdentificationErrors.EmptyFirstName);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', FirstName.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => FirstName.Create(longValue));
        exception.Descriptor.ShouldBe(IdentificationErrors.FirstNameOverflow);
    }

    [Fact]
    public void Create_Should_Accept_MaxLength_Value()
    {
        // Arrange
        var value = new string('a', FirstName.MaxLength);

        // Act
        var name = FirstName.Create(value);

        // Assert
        name.Value.ShouldBe(value);
    }

    [Fact]
    public void MaxLength_Should_Be_24()
    {
        // Assert
        FirstName.MaxLength.ShouldBe(24);
    }
}

public class LastNameTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var name = LastName.Create("Doe");

        // Assert
        name.Value.ShouldBe("Doe");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var name = LastName.Create("  Doe  ");

        // Assert
        name.Value.ShouldBe("Doe");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => LastName.Create(value!));
        exception.Descriptor.ShouldBe(IdentificationErrors.EmptyLastName);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', LastName.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => LastName.Create(longValue));
        exception.Descriptor.ShouldBe(IdentificationErrors.LastNameOverflow);
    }

    [Fact]
    public void Create_Should_Accept_MaxLength_Value()
    {
        // Arrange
        var value = new string('a', LastName.MaxLength);

        // Act
        var name = LastName.Create(value);

        // Assert
        name.Value.ShouldBe(value);
    }

    [Fact]
    public void MaxLength_Should_Be_24()
    {
        // Assert
        LastName.MaxLength.ShouldBe(24);
    }
}

public class FullNameTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var name = FullName.Create("John Doe");

        // Assert
        name.Value.ShouldBe("John Doe");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var name = FullName.Create("  John Doe  ");

        // Assert
        name.Value.ShouldBe("John Doe");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => FullName.Create(value!));
        exception.Descriptor.ShouldBe(IdentificationErrors.EmptyFullName);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', FullName.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => FullName.Create(longValue));
        exception.Descriptor.ShouldBe(IdentificationErrors.FullNameOverflow);
    }

    [Fact]
    public void Create_Should_Accept_MaxLength_Value()
    {
        // Arrange
        var value = new string('a', FullName.MaxLength);

        // Act
        var name = FullName.Create(value);

        // Assert
        name.Value.ShouldBe(value);
    }

    [Fact]
    public void MaxLength_Should_Be_100()
    {
        // Assert
        FullName.MaxLength.ShouldBe(100);
    }
}
