using System.Diagnostics;
using System.Text;
using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests.Redaction;

/// <summary>
/// Request headers, bodies and messages reach the redactors from anonymous clients, so redaction must stay linear
/// on adversarial text. A backtracking pattern takes seconds on these inputs; a linear one a few milliseconds.
/// </summary>
public class RedactionComplexityTests : EksenUnitTestBase
{
    private const int AdversarialLength = 128 * 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(500);

    public static TheoryData<string, string> AdversarialInputs()
    {
        return new TheoryData<string, string>
        {
            { "xml element name", "<" + Repeat("token") },
            { "xml element name with a trailing slash", "<" + Repeat("token") + "/" },
            { "xml namespace", "<" + Repeat("a.") },
            { "xml cdata openings", Repeat("<a><![CDATA[") },
            { "json key", "\"" + Repeat("token") },
            { "json key with spaces", "\"token\"" + Repeat(" ") },
            { "escaped json key", "\\\"" + Repeat("token") },
            { "backslash run", Repeat("\\") },
            { "escaped json value of backslashes", "\\\"token\\\":\\\"" + Repeat("\\") },
            { "xml attribute name", " " + Repeat("token") },
            { "form field name", "&" + Repeat("token") },
            { "leading form field name", Repeat("token") + "/" },
            { "query separators", Repeat("?a") },
            { "query key ending in another character", "?" + Repeat("token") + "/=x" },
            { "session keys", "<" + Repeat("session") },
            { "license keys", "\"" + Repeat("license") },
            { "e-mail local part without a domain", Repeat("a.") + "@" },
            { "e-mail domain without a dot", "a@" + Repeat("b") },
            { "e-mail at signs", Repeat("a@") }
        };
    }

    [Theory]
    [MemberData(nameof(AdversarialInputs))]
    public void Body_Redaction_Should_Stay_Linear(string description, string input)
    {
        AssertWithinBudget(description, () => SensitiveBodyRedactor.Redact(input, "application/x-www-form-urlencoded"));
    }

    [Theory]
    [MemberData(nameof(AdversarialInputs))]
    public void Query_Redaction_Should_Stay_Linear(string description, string input)
    {
        AssertWithinBudget(description, () => SensitiveUrlRedactor.Redact(input));
    }

    [Theory]
    [MemberData(nameof(AdversarialInputs))]
    public void Text_Redaction_Should_Stay_Linear(string description, string input)
    {
        AssertWithinBudget(description, () => TelemetryValueRedactor.RedactText(input));
    }

    [Theory]
    [InlineData("token")]
    [InlineData("password")]
    [InlineData("session")]
    public void Name_Checks_Should_Stay_Linear(string word)
    {
        var name = Repeat(word) + "/";

        AssertWithinBudget(word, () => SensitiveFieldName.IsSensitive(name));
        AssertWithinBudget(word, () => TelemetryValueRedactor.IsSensitiveName(name));
    }

    [Fact]
    public void Text_Longer_Than_The_Limit_Should_Be_Truncated_Before_Redaction()
    {
        var input = "{\"password\":\"" + new string('x', TelemetryValueRedactor.MaxTextLength * 2) + "\"}";

        var redacted = TelemetryValueRedactor.RedactText(input);

        redacted.ShouldBe("{\"password\":\"[redacted]");
    }

    [Fact]
    public void Text_Within_The_Limit_Should_Not_Be_Truncated()
    {
        var input = new string('x', TelemetryValueRedactor.MaxTextLength);

        TelemetryValueRedactor.RedactText(input).ShouldBeSameAs(input);
    }

    [Fact]
    public void Truncation_Should_Not_Split_A_Surrogate_Pair()
    {
        TelemetryValueRedactor.Truncate("ab\U0001F600cd", 3).ShouldBe("ab" + TelemetryValueRedactor.TruncationMarker);
    }

    [Theory]
    [InlineData("{\"note\":\"say \\\"hi\\\"\",\"password\":\"p\"}", "{\"note\":\"say \\\"hi\\\"\",\"password\":\"[redacted]\"}")]
    [InlineData("{\"a\": \"token\": \"x\"}", "{\"a\": \"token\": \"[redacted]\"}")]
    [InlineData("<note>a</note><ns:apiKey a=\"1\">k</ns:apiKey>", "<note>a</note><ns:apiKey a=\"1\">[redacted]</ns:apiKey>")]
    [InlineData("<a.password>k</a.password>", "<a.password>k</a.password>")]
    [InlineData("\\\\\"note\\\\\":\\\\\"a\\\\\",\\\\\"token\\\\\":\\\\\"b\\\\\"", "\\\\\"note\\\\\":\\\\\"a\\\\\",\\\\\"token\\\\\":\\\\\"[redacted]\\\\\"")]
    public void Keys_After_Other_Keys_Should_Still_Be_Redacted(string input, string expected)
    {
        SensitiveBodyRedactor.Redact(input).ShouldBe(expected);
    }

    private static string Repeat(string unit)
    {
        var builder = new StringBuilder(AdversarialLength + unit.Length);
        while (builder.Length < AdversarialLength)
        {
            builder.Append(unit);
        }

        return builder.ToString();
    }

    private static void AssertWithinBudget(string description, Action redact)
    {
        // Warm up the generated matchers so the measurement covers matching only.
        redact();

        var stopwatch = Stopwatch.StartNew();
        redact();
        stopwatch.Stop();

        stopwatch.Elapsed.ShouldBeLessThan(Budget, $"Redacting the {description} input took {stopwatch.Elapsed.TotalMilliseconds} ms.");
    }
}
