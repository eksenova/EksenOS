using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests.Redaction;

// Ported from the log-event enricher tests: the same guarantees now hold for every telemetry value.
public class TelemetryValueRedactorTests : EksenUnitTestBase
{
    [Fact]
    public void Masks_Sensitive_Query_Values_In_A_Url()
    {
        var redacted = TelemetryValueRedactor.Redact("url.full", "https://bank.test/token?client_id=abc&client_secret=hidden");

        redacted.ShouldNotContain("hidden");
        redacted.ShouldContain("client_id=abc");
    }

    [Fact]
    public void Masks_Bodies_Carried_As_String_Values()
    {
        var body = TelemetryValueRedactor.Redact("eksen.http.response_body", "{\"access_token\":\"hidden\",\"expires_in\":3600}");

        body.ShouldNotContain("hidden");
        body.ShouldContain("expires_in");
    }

    [Fact]
    public void Masks_A_Pretty_Printed_Body()
    {
        TelemetryValueRedactor.RedactText("{\n  \"access_token\": \"hidden\",\n  \"token_type\": \"Bearer\"\n}")
            .ShouldNotContain("hidden");
    }

    [Theory]
    [InlineData("eksen.http.query.token")]
    [InlineData("password")]
    [InlineData("http.request.header.x-api-key")]
    [InlineData("client_secret")]
    public void Replaces_Values_Under_Sensitive_Names(string name)
    {
        TelemetryValueRedactor.Redact(name, "plain-looking-value").ShouldBe("[redacted]");
    }

    [Theory]
    [InlineData("Account", "TR330006100519786457841326")]
    [InlineData("eksen.api_key_id", "01KNSCSM2F165RX7V665KNDXJ8")]
    [InlineData("messaging.rabbitmq.destination.routing_key", "orders.placed")]
    [InlineData("user_id", "42")]
    public void Leaves_Other_Values_Untouched(string name, string value)
    {
        TelemetryValueRedactor.Redact(name, value).ShouldBe(value);
    }

    [Theory]
    [InlineData("Mailbox unavailable for <alice.smith+shop@mail.example.co.uk>: 550", "Mailbox unavailable for <[email]>: 550")]
    [InlineData("duplicate key (email)=(bob@example.com) violates", "duplicate key (email)=([email]) violates")]
    [InlineData("send to a@b.io and c@d.io", "send to [email] and [email]")]
    public void Replaces_Email_Addresses_In_Text(string input, string expected)
    {
        TelemetryValueRedactor.RedactText(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("user@localhost")]
    [InlineData("@channel")]
    [InlineData("v1.2.3@sha256")]
    public void Leaves_Text_That_Is_Not_An_Email_Address(string input)
    {
        TelemetryValueRedactor.RedactText(input).ShouldBe(input);
    }

    [Theory]
    [InlineData("https://bucket.s3.test/a?X-Amz-Credential=AKIA%2F20261112&X-Amz-Signature=abc123&X-Amz-Expires=900")]
    public void Masks_Presigned_Url_Signature_And_Credential(string url)
    {
        var redacted = TelemetryValueRedactor.Redact("url.full", url);

        redacted.ShouldNotContain("abc123");
        redacted.ShouldNotContain("AKIA");
        redacted.ShouldContain("X-Amz-Expires=900");
    }
}
