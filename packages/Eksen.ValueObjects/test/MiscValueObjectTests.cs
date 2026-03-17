using Eksen.ErrorHandling;
using Eksen.TestBase;
using Eksen.ValueObjects.GeoLocation;
using Eksen.ValueObjects.Hashing;
using Eksen.ValueObjects.Http;
using Eksen.ValueObjects.Comments;
using Shouldly;

namespace Eksen.ValueObjects.Tests;

public class AddressLineTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var address = AddressLine.Create("123 Main Street, Suite 100");

        // Assert
        address.Value.ShouldBe("123 Main Street, Suite 100");
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var address = AddressLine.Create("  123 Main Street  ");

        // Assert
        address.Value.ShouldBe("123 Main Street");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => AddressLine.Create(value!));
        exception.Descriptor.ShouldBe(GeoLocationErrors.EmptyAddressLine);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', AddressLine.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => AddressLine.Create(longValue));
        exception.Descriptor.ShouldBe(GeoLocationErrors.AddressLineOverflow);
    }

    [Fact]
    public void MaxLength_Should_Be_255()
    {
        // Assert
        AddressLine.MaxLength.ShouldBe(255);
    }
}

public class PasswordHashTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange
        var hash = "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

        // Act
        var passwordHash = PasswordHash.Create(hash);

        // Assert
        passwordHash.Value.ShouldBe(hash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => PasswordHash.Create(value!));
        exception.Descriptor.ShouldBe(HashingErrors.EmptyHash);
    }

    [Fact]
    public void MaxLength_Should_Be_256()
    {
        // Assert
        PasswordHash.MaxLength.ShouldBe(256);
    }
}

public class UserAgentTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange
        var ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        // Act
        var userAgent = UserAgent.Create(ua);

        // Assert
        userAgent.Value.ShouldBe(ua);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Should_Throw_When_Null_Or_Whitespace(string? value)
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<EksenException>(() => UserAgent.Create(value!));
        exception.Descriptor.ShouldBe(HttpErrors.EmptyUserAgent);
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', UserAgent.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => UserAgent.Create(longValue));
        exception.Descriptor.ShouldBe(HttpErrors.UserAgentOverflow);
    }

    [Fact]
    public void MaxLength_Should_Be_255()
    {
        // Assert
        UserAgent.MaxLength.ShouldBe(255);
    }
}

public class ActionCommentTests : EksenUnitTestBase
{
    [Fact]
    public void Create_Should_Be_Successful()
    {
        // Arrange & Act
        var comment = ActionComment.Create("This is a comment");

        // Assert
        comment.Value.ShouldBe("This is a comment");
    }

    [Fact]
    public void Create_Should_Return_Empty_When_Null_Or_Whitespace()
    {
        // Arrange & Act
        var comment = ActionComment.Create("");

        // Assert
        comment.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public void Empty_Should_Return_Empty_Comment()
    {
        // Arrange & Act
        var comment = ActionComment.Empty;

        // Assert
        comment.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public void Create_Should_Trim_Whitespace()
    {
        // Arrange & Act
        var comment = ActionComment.Create("  Hello World  ");

        // Assert
        comment.Value.ShouldBe("Hello World");
    }

    [Fact]
    public void Create_Should_Throw_When_Exceeds_MaxLength()
    {
        // Arrange
        var longValue = new string('a', ActionComment.MaxLength + 1);

        // Act & Assert
        var exception = Should.Throw<EksenException>(() => ActionComment.Create(longValue));
        exception.Descriptor.ShouldBe(CommentErrors.ActionCommentTooLong);
    }

    [Fact]
    public void MaxLength_Should_Be_2000()
    {
        // Assert
        ActionComment.MaxLength.ShouldBe(2000);
    }

    [Fact]
    public void Create_Should_Accept_MaxLength_Value()
    {
        // Arrange
        var value = new string('a', ActionComment.MaxLength);

        // Act
        var comment = ActionComment.Create(value);

        // Assert
        comment.Value.ShouldBe(value);
    }
}
