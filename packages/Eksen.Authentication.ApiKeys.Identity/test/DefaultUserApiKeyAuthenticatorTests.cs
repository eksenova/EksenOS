using System.Security.Claims;
using Eksen.Authentication.ApiKeys.Identity.Tests.Fakes;
using Eksen.Identity.Claims;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Moq;
using Shouldly;

namespace Eksen.Authentication.ApiKeys.Identity.Tests;

public class DefaultUserApiKeyAuthenticatorTests : EksenUnitTestBase
{
    private readonly Mock<IEksenUserApiKeyRepository<FakeUser, FakeTenant>> _repositoryMock = new();

    private DefaultUserApiKeyAuthenticator<FakeUser, FakeTenant> CreateAuthenticator()
    {
        return new DefaultUserApiKeyAuthenticator<FakeUser, FakeTenant>(_repositoryMock.Object);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Return_Success_For_Valid_Key()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = EmailAddress.Create("test@test.com") };
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            ApiKeyName.Create("Test Key"),
            ApiKeyValue.Create("valid-key"),
            user, null, null);

        _repositoryMock
            .Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKey);

        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("valid-key");

        // Assert
        result.IsAuthenticated.ShouldBeTrue();
        result.Principal.ShouldNotBeNull();
        result.Principal.FindFirst(ClaimTypes.NameIdentifier)!.Value
            .ShouldBe(user.Id.Value.ToString());
        result.Principal.FindFirst(ClaimTypes.Email)!.Value
            .ShouldBe("test@test.com");
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Return_Failure_For_Invalid_Format()
    {
        // Arrange
        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("");

        // Assert
        result.IsAuthenticated.ShouldBeFalse();
        result.FailureReason!.ShouldContain("Invalid");
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Return_Failure_When_Key_Not_Found()
    {
        // Arrange
        _repositoryMock
            .Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EksenUserApiKey<FakeUser, FakeTenant>?)null);

        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("unknown-key");

        // Assert
        result.IsAuthenticated.ShouldBeFalse();
        result.FailureReason!.ShouldContain("not found");
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Return_Failure_When_Key_Is_Revoked()
    {
        // Arrange
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            ApiKeyName.Create("Test Key"),
            ApiKeyValue.Create("revoked-key"),
            new FakeUser(), null, null);
        apiKey.Revoke();

        _repositoryMock
            .Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKey);

        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("revoked-key");

        // Assert
        result.IsAuthenticated.ShouldBeFalse();
        result.FailureReason!.ShouldContain("revoked");
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Return_Failure_When_Key_Is_Expired()
    {
        // Arrange
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            ApiKeyName.Create("Test Key"),
            ApiKeyValue.Create("expired-key"),
            new FakeUser(), null, DateTime.UtcNow.AddDays(-1));

        _repositoryMock
            .Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKey);

        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("expired-key");

        // Assert
        result.IsAuthenticated.ShouldBeFalse();
        result.FailureReason!.ShouldContain("expired");
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Return_Failure_When_User_Is_Inactive()
    {
        // Arrange
        var user = new FakeUser { IsActive = false };
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            ApiKeyName.Create("Test Key"),
            ApiKeyValue.Create("inactive-user-key"),
            user, null, null);

        _repositoryMock
            .Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKey);

        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("inactive-user-key");

        // Assert
        result.IsAuthenticated.ShouldBeFalse();
        result.FailureReason!.ShouldContain("not active");
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Include_Tenant_Claims_When_Tenant_Present()
    {
        // Arrange
        var tenant = new FakeTenant();
        var user = new FakeUser { Tenant = tenant };
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            ApiKeyName.Create("Test Key"),
            ApiKeyValue.Create("tenant-key"),
            user, tenant, null);

        _repositoryMock
            .Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKey);

        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("tenant-key");

        // Assert
        result.IsAuthenticated.ShouldBeTrue();
        result.Principal!.FindFirst(EksenClaims.TenantId)!.Value
            .ShouldBe(tenant.Id.Value.ToString());
        result.Principal.FindFirst(EksenClaims.TenantName)!.Value
            .ShouldBe(tenant.Name.Value);
    }

    [Fact]
    public async Task AuthenticateAsync_Should_Not_Include_Email_Claim_When_No_Email()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = null };
        var apiKey = new EksenUserApiKey<FakeUser, FakeTenant>(
            ApiKeyName.Create("Test Key"),
            ApiKeyValue.Create("no-email-key"),
            user, null, null);

        _repositoryMock
            .Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKey);

        var authenticator = CreateAuthenticator();

        // Act
        var result = await authenticator.AuthenticateAsync("no-email-key");

        // Assert
        result.IsAuthenticated.ShouldBeTrue();
        result.Principal!.FindFirst(ClaimTypes.Email).ShouldBeNull();
    }
}
