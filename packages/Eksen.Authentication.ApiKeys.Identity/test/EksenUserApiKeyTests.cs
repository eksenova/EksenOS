using Eksen.Authentication.ApiKeys.Identity.Tests.Fakes;
using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Identity.Tests;

public class EksenUserApiKeyTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_ApiKey_With_Required_Fields()
    {
        // Arrange
        var name = ApiKeyName.Create("Test Key");
        var keyValue = ApiKeyValue.Create("test-api-key-value");
        var user = new FakeUser { EmailAddress = EmailAddress.Create("test@test.com") };

        // Act
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            name, keyValue, user, null, null);

        // Assert
        apiKey.Id.ShouldNotBe(EksenUserApiKeyId.Empty);
        apiKey.Name.ShouldBe(name);
        apiKey.KeyValue.ShouldBe(keyValue);
        apiKey.User.ShouldBe(user);
        apiKey.Tenant.ShouldBeNull();
        apiKey.ExpiresAt.ShouldBeNull();
        apiKey.RevokedAt.ShouldBeNull();
        apiKey.IsActive.ShouldBeTrue();
        apiKey.IsRevoked.ShouldBeFalse();
        apiKey.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_Should_Create_ApiKey_With_Tenant()
    {
        // Arrange
        var name = ApiKeyName.Create("Test Key");
        var keyValue = ApiKeyValue.Create("test-api-key-value");
        var tenant = new FakeTenant();
        var user = new FakeUser { Tenant = tenant };

        // Act
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            name, keyValue, user, tenant, null);

        // Assert
        apiKey.Tenant.ShouldBe(tenant);
    }

    [Fact]
    public void Constructor_Should_Create_ApiKey_With_Expiry()
    {
        // Arrange
        var name = ApiKeyName.Create("Test Key");
        var keyValue = ApiKeyValue.Create("test-api-key-value");
        var user = new FakeUser();
        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            name, keyValue, user, null, expiresAt);

        // Assert
        apiKey.ExpiresAt.ShouldBe(expiresAt);
        apiKey.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_Should_Be_True_When_ExpiresAt_Is_In_Past()
    {
        // Arrange
        var name = ApiKeyName.Create("Test Key");
        var keyValue = ApiKeyValue.Create("test-api-key-value");
        var user = new FakeUser();
        var expiresAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            name, keyValue, user, null, expiresAt);

        // Assert
        apiKey.IsExpired.ShouldBeTrue();
        apiKey.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Revoke_Should_Set_RevokedAt()
    {
        // Arrange
        var apiKey = CreateApiKey();

        // Act
        apiKey.Revoke();

        // Assert
        apiKey.RevokedAt.ShouldNotBeNull();
        apiKey.IsRevoked.ShouldBeTrue();
        apiKey.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Revoke_Should_Throw_When_Already_Revoked()
    {
        // Arrange
        var apiKey = CreateApiKey();
        apiKey.Revoke();

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => apiKey.Revoke());
        exception.Descriptor.ShouldBe(ApiKeyErrors.ApiKeyAlreadyRevoked);
    }

    [Fact]
    public void Regenerate_Should_Update_KeyValue()
    {
        // Arrange
        var apiKey = CreateApiKey();
        var newKeyValue = ApiKeyValue.Create("new-api-key-value");

        // Act
        apiKey.Regenerate(newKeyValue);

        // Assert
        apiKey.KeyValue.ShouldBe(newKeyValue);
    }

    [Fact]
    public void Regenerate_Should_Throw_When_Revoked()
    {
        // Arrange
        var apiKey = CreateApiKey();
        apiKey.Revoke();
        var newKeyValue = ApiKeyValue.Create("new-api-key-value");

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => apiKey.Regenerate(newKeyValue));
        exception.Descriptor.ShouldBe(ApiKeyErrors.ApiKeyRevoked);
    }

    [Fact]
    public void SetName_Should_Update_Name()
    {
        // Arrange
        var apiKey = CreateApiKey();
        var newName = ApiKeyName.Create("Updated Key");

        // Act
        apiKey.SetName(newName);

        // Assert
        apiKey.Name.ShouldBe(newName);
    }

    [Fact]
    public void SetExpiresAt_Should_Update_ExpiresAt()
    {
        // Arrange
        var apiKey = CreateApiKey();
        var newExpiry = DateTime.UtcNow.AddDays(60);

        // Act
        apiKey.SetExpiresAt(newExpiry);

        // Assert
        apiKey.ExpiresAt.ShouldBe(newExpiry);
    }

    private static EksenUserApiKey<FakeUser, FakeTenant> CreateApiKey()
    {
        return new EksenUserApiKey<FakeUser, FakeTenant>(
            ApiKeyName.Create("Test Key"),
            ApiKeyValue.Create("test-api-key-value"),
            new FakeUser(),
            null,
            null);
    }
}
