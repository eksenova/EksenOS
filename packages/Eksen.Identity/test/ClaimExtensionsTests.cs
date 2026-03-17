using System.Security.Claims;
using Eksen.Identity.Claims;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Identity.Tests;

public class ClaimExtensionsTests : EksenUnitTestBase
{
    [Fact]
    public void GetClaim_On_Principal_Should_Return_Value_When_Claim_Exists()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("role", "Admin")]);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var value = principal.GetClaim("role");

        // Assert
        value.ShouldBe("Admin");
    }

    [Fact]
    public void GetClaim_On_Principal_Should_Return_Last_Value_When_Multiple_Claims()
    {
        // Arrange
        var identity = new ClaimsIdentity(
        [
            new Claim("role", "User"),
            new Claim("role", "Admin")
        ]);
        var principal = new ClaimsPrincipal(identity);

        // Act
        var value = principal.GetClaim("role");

        // Assert
        value.ShouldBe("Admin");
    }

    [Fact]
    public void GetClaim_On_Principal_Should_Return_Null_When_Claim_Not_Found()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        // Act
        var value = principal.GetClaim("nonexistent");

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetClaim_On_Principal_Should_Throw_When_Principal_Is_Null()
    {
        // Arrange
        ClaimsPrincipal principal = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => principal.GetClaim("role"));
    }

    [Fact]
    public void GetClaim_On_Principal_Should_Throw_When_Type_Is_Empty()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act & Assert
        Should.Throw<ArgumentException>(() => principal.GetClaim(""));
    }

    [Fact]
    public void GetClaim_On_Identity_Should_Return_Value_When_Claim_Exists()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("role", "Admin")]);

        // Act
        var value = identity.GetClaim("role");

        // Assert
        value.ShouldBe("Admin");
    }

    [Fact]
    public void GetClaim_On_Identity_Should_Return_Null_When_Claim_Not_Found()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        var value = identity.GetClaim("nonexistent");

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void AddIfNotExists_Should_Add_When_Claim_Not_Present()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        identity.AddIfNotExists(new Claim("role", "Admin"));

        // Assert
        identity.FindFirst("role").ShouldNotBeNull();
        identity.FindFirst("role")!.Value.ShouldBe("Admin");
    }

    [Fact]
    public void AddIfNotExists_Should_Not_Add_When_Claim_Already_Present()
    {
        // Arrange
        var identity = new ClaimsIdentity([new Claim("role", "User")]);

        // Act
        identity.AddIfNotExists(new Claim("role", "Admin"));

        // Assert
        identity.FindAll("role").Count().ShouldBe(1);
        identity.FindFirst("role")!.Value.ShouldBe("User");
    }

    [Fact]
    public void AddIfNotExists_Should_Return_Same_Identity()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        var returned = identity.AddIfNotExists(new Claim("role", "Admin"));

        // Assert
        returned.ShouldBeSameAs(identity);
    }

    [Fact]
    public void AddIfNotExists_Should_Throw_When_Claim_Is_Null()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => identity.AddIfNotExists(null!));
    }

    [Fact]
    public void AddOrReplace_Should_Add_When_No_Existing_Claim()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        identity.AddOrReplace(new Claim("role", "Admin"));

        // Assert
        identity.FindFirst("role").ShouldNotBeNull();
        identity.FindFirst("role")!.Value.ShouldBe("Admin");
    }

    [Fact]
    public void AddOrReplace_Should_Replace_Existing_Claims()
    {
        // Arrange
        var identity = new ClaimsIdentity(
        [
            new Claim("role", "User"),
            new Claim("role", "Editor")
        ]);

        // Act
        identity.AddOrReplace(new Claim("role", "Admin"));

        // Assert
        identity.FindAll("role").Count().ShouldBe(1);
        identity.FindFirst("role")!.Value.ShouldBe("Admin");
    }

    [Fact]
    public void AddOrReplace_Should_Return_Same_Identity()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act
        var returned = identity.AddOrReplace(new Claim("role", "Admin"));

        // Assert
        returned.ShouldBeSameAs(identity);
    }

    [Fact]
    public void AddOrReplace_Should_Throw_When_Claim_Is_Null()
    {
        // Arrange
        var identity = new ClaimsIdentity();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => identity.AddOrReplace(null!));
    }
}
