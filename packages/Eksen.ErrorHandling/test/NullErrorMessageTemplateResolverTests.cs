using Eksen.TestBase;
using Shouldly;

namespace Eksen.ErrorHandling.Tests;

public class NullErrorMessageTemplateResolverTests : EksenUnitTestBase
{
    [Fact]
    public void ResolveErrorMessageTemplate_Should_Return_Code_As_Is()
    {
        // Arrange
        var resolver = new NullErrorMessageTemplateResolver();

        // Act
        var result = resolver.ResolveErrorMessageTemplate("Orders.NotFound");

        // Assert
        result.ShouldBe("Orders.NotFound");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Some.Error.Code")]
    [InlineData("SimpleCode")]
    public void ResolveErrorMessageTemplate_Should_Return_Input_Unchanged(string code)
    {
        // Arrange
        var resolver = new NullErrorMessageTemplateResolver();

        // Act
        var result = resolver.ResolveErrorMessageTemplate(code);

        // Assert
        result.ShouldBe(code);
    }
}
