using Eksen.TestBase;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Tests;

public class ApiKeyErrorsTests : EksenUnitTestBase
{
    [Fact]
    public void Category_Should_Be_Correct()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.Category.ShouldBe("Eksen.Authentication.ApiKeys");
    }

    [Fact]
    public void EmptyApiKeyValue_Should_Be_Validation_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.EmptyApiKeyValue.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.Validation);
    }

    [Fact]
    public void ApiKeyValueOverflow_Should_Be_Validation_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.ApiKeyValueOverflow.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.Validation);
    }

    [Fact]
    public void EmptyApiKeyName_Should_Be_Validation_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.EmptyApiKeyName.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.Validation);
    }

    [Fact]
    public void ApiKeyNameOverflow_Should_Be_Validation_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.ApiKeyNameOverflow.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.Validation);
    }

    [Fact]
    public void ApiKeyRevoked_Should_Be_Authorization_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.ApiKeyRevoked.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.Authorization);
    }

    [Fact]
    public void ApiKeyExpired_Should_Be_Authorization_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.ApiKeyExpired.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.Authorization);
    }

    [Fact]
    public void ApiKeyNotFound_Should_Be_NotFound_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.ApiKeyNotFound.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.NotFound);
    }

    [Fact]
    public void ApiKeyAlreadyRevoked_Should_Be_Validation_Error()
    {
        // Arrange & Act & Assert
        ApiKeyErrors.ApiKeyAlreadyRevoked.ErrorType.ShouldBe(Eksen.ErrorHandling.ErrorType.Validation);
    }
}
