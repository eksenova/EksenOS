using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests.Redaction;

public class HeaderRedactorTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("Authorization", "Bot placeholder-bot-credential")]
    [InlineData("authorization", "Bearer eyJhbGciOiJIUzI1NiJ9.e30.abc")]
    [InlineData("Cookie", "session=abc")]
    [InlineData("X-API-KEY", "abc")]
    [InlineData("x-csrf-token", "abc")]
    [InlineData("Ocp-Apim-Subscription-Key", "abc")]
    public void Masks_Default_Sensitive_Headers(string header, string value)
    {
        var redactor = new HeaderRedactor();

        redactor.IsSensitive(header).ShouldBeTrue();
        redactor.Redact(header, value).ShouldBe("***");
    }

    [Fact]
    public void Masks_Configured_Additional_Headers()
    {
        var redactor = new HeaderRedactor([" X-Signature-Ed25519 ", "", "X-Discord-Token"]);

        redactor.Redact("x-signature-ed25519", "abc").ShouldBe("***");
        redactor.Redact("X-Discord-Token", "abc").ShouldBe("***");
    }

    [Theory]
    [InlineData("X-Discord-Bot-Key")]
    [InlineData("x-discord-bot-key")]
    [InlineData("X-Foo-Secret")]
    [InlineData("X-License-Key")]
    [InlineData("X-Session")]
    [InlineData("X_Internal_Token")]
    [InlineData("X-Upstream-Password")]
    public void Masks_Headers_With_A_Credential_Segment_Or_A_Sensitive_Name(string header)
    {
        var redactor = new HeaderRedactor();

        redactor.IsSensitive(header).ShouldBeTrue();
        redactor.Redact(header, "botsecret123").ShouldBe("***");
    }

    [Theory]
    [InlineData("Accept", "application/json")]
    [InlineData("Keep-Alive", "timeout=5")]
    [InlineData("X-Keyboard-Layout", "tr")]
    [InlineData("X-Request-Id", "abc")]
    [InlineData("User-Agent", "client/1.0")]
    public void Leaves_Other_Headers_Unchanged(string header, string value)
    {
        new HeaderRedactor().Redact(header, value).ShouldBe(value);
    }
}
