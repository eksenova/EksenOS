using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Net;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class IpV4AddressTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("10.0.0.1")]
    public void Create_Should_Be_Successful(string value)
    {
        // Arrange & Act
        var ip = IpV4Address.Create(value);

        // Assert
        ip.Value.ShouldBe(value);
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var ip = IpV4Address.Create("  192.168.1.1  ");

        // Assert
        ip.Value.ShouldBe("192.168.1.1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => IpV4Address.Create(value!));
        exception.Descriptor.ShouldBe(NetErrors.EmptyIpAddress);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('1', IpV4Address.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => IpV4Address.Create(longValue));
        exception.Descriptor.ShouldBe(NetErrors.IpV4AddressOverflow);
    }

    [Theory]
    [InlineData("999.999.999.999")]
    [InlineData("abc.def.ghi.jkl")]
    [InlineData("256.1.1.1")]
    public void Create_Should_Throw_When_Invalid_Format(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => IpV4Address.Create(value));
        exception.Descriptor.ShouldBe(NetErrors.InvalidIpV4Address);
    }

    [Fact]
    public void MaxLength_Should_Be_15()
    {
        // Assert
        IpV4Address.MaxLength.ShouldBe(15);
    }

    [Fact]
    public void ToParseableString_Should_Return_Value()
    {
        // Arrange
        var ip = IpV4Address.Create("192.168.1.1");

        // Act
        var result = ip.ToParseableString();

        // Assert
        result.ShouldBe("192.168.1.1");
    }
}

public class PortTests : EksenUnitTestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    [InlineData(443)]
    [InlineData(8080)]
    [InlineData(65535)]
    public void Create_Should_Be_Successful(int value)
    {
        // Arrange & Act
        var port = Port.Create(value);

        // Assert
        port.Value.ShouldBe(value);
    }

    [Fact]
    public void Create_Should_Throw_When_Below_MinValue()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => Port.Create(-1));
    }

    [Fact]
    public void Create_Should_Throw_When_Above_MaxValue()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => Port.Create(65536));
    }

    [Fact]
    public void Parse_Should_Return_Port()
    {
        // Arrange & Act
        var port = Port.Parse("8080");

        // Assert
        port.Value.ShouldBe(8080);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("not-a-number")]
    public void Parse_Should_Throw_When_Invalid(string value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => Port.Parse(value));
        exception.Descriptor.ShouldBe(NetErrors.InvalidPort);
    }

    [Fact]
    public void MinValue_Should_Be_0()
    {
        // Assert
        Port.MinValue.ShouldBe(0);
    }

    [Fact]
    public void MaxValue_Should_Be_65535()
    {
        // Assert
        Port.MaxValue.ShouldBe(65535);
    }
}
