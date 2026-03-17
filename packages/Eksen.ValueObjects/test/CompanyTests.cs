using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Companies;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class CompanyNameTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var name = CompanyName.Create("Acme Corp");

        // Assert
        name.Value.ShouldBe("Acme Corp");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var name = CompanyName.Create("  Acme Corp  ");

        // Assert
        name.Value.ShouldBe("Acme Corp");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => CompanyName.Create(value!));
        exception.Descriptor.ShouldBe(CompanyErrors.EmptyCompanyName);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', CompanyName.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => CompanyName.Create(longValue));
        exception.Descriptor.ShouldBe(CompanyErrors.CompanyNameOverflow);
    }

    [Fact]
    public void MaxLength_Should_Be_50()
    {
        // Assert
        CompanyName.MaxLength.ShouldBe(50);
    }
}

public class CompanyTitleTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var title = CompanyTitle.Create("Acme Corporation Ltd.");

        // Assert
        title.Value.ShouldBe("Acme Corporation Ltd.");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var title = CompanyTitle.Create("  Acme Corporation  ");

        // Assert
        title.Value.ShouldBe("Acme Corporation");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => CompanyTitle.Create(value!));
        exception.Descriptor.ShouldBe(CompanyErrors.EmptyCompanyTitle);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', CompanyTitle.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => CompanyTitle.Create(longValue));
        exception.Descriptor.ShouldBe(CompanyErrors.CompanyTitleOverflow);
    }

    [Fact]
    public void MaxLength_Should_Be_200()
    {
        // Assert
        CompanyTitle.MaxLength.ShouldBe(200);
    }
}

public class TaxNumberTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var taxNumber = TaxNumber.Create("1234567890");

        // Assert
        taxNumber.Value.ShouldBe("1234567890");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var taxNumber = TaxNumber.Create("  1234567890  ");

        // Assert
        taxNumber.Value.ShouldBe("1234567890");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => TaxNumber.Create(value!));
        exception.Descriptor.ShouldBe(CompanyErrors.EmptyTaxNumber);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('1', TaxNumber.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => TaxNumber.Create(longValue));
        exception.Descriptor.ShouldBe(CompanyErrors.TaxNumberOverflow);
    }

    [Fact]
    public void MaxLength_Should_Be_20()
    {
        // Assert
        TaxNumber.MaxLength.ShouldBe(20);
    }
}

public class TaxOfficeTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var taxOffice = TaxOffice.Create("Main Office");

        // Assert
        taxOffice.Value.ShouldBe("Main Office");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var taxOffice = TaxOffice.Create("  Main Office  ");

        // Assert
        taxOffice.Value.ShouldBe("Main Office");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => TaxOffice.Create(value!));
        exception.Descriptor.ShouldBe(CompanyErrors.EmptyTaxOffice);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', TaxOffice.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => TaxOffice.Create(longValue));
        exception.Descriptor.ShouldBe(CompanyErrors.TaxOfficeOverflow);
    }

    [Fact]
    public void MaxLength_Should_Be_100()
    {
        // Assert
        TaxOffice.MaxLength.ShouldBe(100);
    }
}
