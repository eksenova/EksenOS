using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests.Redaction;

public class ClientLogScrubberTests : EksenUnitTestBase
{
    [Fact]
    public void Clean_Should_Remove_Personal_Data_And_Url_Suffixes()
    {
        var cleaned = ClientLogScrubber.Clean(
            "Failed https://shop.test/checkout?session=abc#frag for alice@example.com token eyJhbGciOiJIUzI1NiJ9.e30.sig phone 05321234567\u0007",
            maxLength: 500);

        cleaned.ShouldBe("Failed https://shop.test/checkout for [redacted] token [redacted] phone [redacted]");
    }

    [Fact]
    public void Clean_Should_Bound_The_Length_And_Keep_Lines_On_Request()
    {
        ClientLogScrubber.Clean(new string('a', 50), maxLength: 10).ShouldBe(new string('a', 10));
        ClientLogScrubber.Clean("line1\nline2\t", maxLength: 100, keepLines: true).ShouldBe("line1\nline2");
        ClientLogScrubber.Clean("line1\nline2", maxLength: 100).ShouldBe("line1 line2");
        ClientLogScrubber.Clean("   ", maxLength: 10).ShouldBeNull();
    }
}
