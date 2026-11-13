using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests.Redaction;

public class SensitiveUrlRedactorTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("/c/?token=secret", "/c/?token=[redacted]")]
    [InlineData("/c/?foo=bar&token=secret&next=yes", "/c/?foo=bar&token=[redacted]&next=yes")]
    [InlineData("<a href=\"https://example.test/c/?token=secret\">A</a>", "<a href=\"https://example.test/c/?token=[redacted]\">A</a>")]
    [InlineData("/c/?foo=bar&amp;TOKEN=secret", "/c/?foo=bar&amp;TOKEN=[redacted]")]
    [InlineData("/c/?%74oken=secret", "/c/?%74oken=[redacted]")]
    // A URL signature (S3/MinIO presigned X-Amz-Signature, signed download links) authorises the request on its own until it
    // expires, so it is treated as a credential.
    [InlineData("/api/files?signature=abc&name=document.pdf", "/api/files?signature=[redacted]&name=document.pdf")]
    [InlineData("/b/o?X-Amz-Credential=AKIA%2F1&X-Amz-Date=20261112T000000Z&X-Amz-Signature=abc", "/b/o?X-Amz-Credential=[redacted]&X-Amz-Date=20261112T000000Z&X-Amz-Signature=[redacted]")]
    [InlineData("/oauth/v2/token?client_id=abc&client_secret=hidden&grant_type=x", "/oauth/v2/token?client_id=abc&client_secret=[redacted]&grant_type=x")]
    [InlineData("/login?user=alice&password=hidden", "/login?user=alice&password=[redacted]")]
    [InlineData("/data?page=1&apikey=abc", "/data?page=1&apikey=[redacted]")]
    [InlineData("/c/?%zztoken=secret", "/c/?%zztoken=secret")]
    [InlineData("/api/registrations?licenseKey=0f8fad5b-d9cb-469f-a165-70867728950e&take=20", "/api/registrations?licenseKey=[redacted]&take=20")]
    [InlineData("/api/registrations?license_key=0f8fad5b&take=20", "/api/registrations?license_key=[redacted]&take=20")]
    [InlineData("/api/registrations?LicenceKey=0f8fad5b", "/api/registrations?LicenceKey=[redacted]")]
    public void Redacts_Sensitive_Query_Values_Without_Changing_Other_Values(string input, string expected)
    {
        SensitiveUrlRedactor.Redact(input).ShouldBe(expected);
    }
}
