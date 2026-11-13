using Eksen.Telemetry.Redaction;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Telemetry.Tests.Redaction;

public class SensitiveBodyRedactorTests : EksenUnitTestBase
{
    [Theory]
    [InlineData("{\"password\":\"hidden\",\"userName\":\"alice\"}", "{\"password\":\"[redacted]\",\"userName\":\"alice\"}")]
    [InlineData("{\"Password\" : \"hi\\\"dden\"}", "{\"Password\" : \"[redacted]\"}")]
    [InlineData("{\"access_token\":\"abc\",\"expires_in\":3600}", "{\"access_token\":\"[redacted]\",\"expires_in\":3600}")]
    [InlineData("{\"refresh_token\":\"abc\"}", "{\"refresh_token\":\"[redacted]\"}")]
    [InlineData("{\"clientSecret\":\"abc\"}", "{\"clientSecret\":\"[redacted]\"}")]
    [InlineData("{\"AppKey\":\"abc\",\"MessageKey\":\"note\"}", "{\"AppKey\":\"[redacted]\",\"MessageKey\":\"note\"}")]
    [InlineData("{\"ChannelSessionId\":\"abc\"}", "{\"ChannelSessionId\":\"[redacted]\"}")]
    [InlineData("{\"key\":\"abc\"}", "{\"key\":\"[redacted]\"}")]
    [InlineData("{\"pin\":\"1\",\"password\":12345}", "{\"pin\":\"1\",\"password\":\"[redacted]\"}")]
    [InlineData("{\"password\":null}", "{\"password\":null}")]
    [InlineData("{\"licenseKey\":\"0f8fad5b-d9cb-469f-a165-70867728950e\",\"take\":20}", "{\"licenseKey\":\"[redacted]\",\"take\":20}")]
    [InlineData("{\"license_key\":\"0f8fad5b\"}", "{\"license_key\":\"[redacted]\"}")]
    public void Redacts_Json_Fields(string input, string expected)
    {
        SensitiveBodyRedactor.Redact(input).ShouldBe(expected);
    }

    [Fact]
    public void Redacts_A_Json_Value_Cut_Off_By_Truncation()
    {
        SensitiveBodyRedactor.Redact("{\"access_token\":\"abcdef...[truncated]")
            .ShouldBe("{\"access_token\":\"[redacted]");
    }

    [Fact]
    public void Redacts_Json_Serialized_Inside_A_String()
    {
        SensitiveBodyRedactor.Redact("{\"authJson\":\"{\\\"Password\\\":\\\"hidden\\\",\\\"UserName\\\":\\\"alice\\\"}\"}")
            .ShouldBe("{\"authJson\":\"{\\\"Password\\\":\\\"[redacted]\\\",\\\"UserName\\\":\\\"alice\\\"}\"}");
    }

    [Theory]
    [InlineData("<USER>alice</USER><PASSWORD>hidden</PASSWORD>", "<USER>alice</USER><PASSWORD>[redacted]</PASSWORD>")]
    [InlineData("<web:Password xsi:type=\"s\">hidden</web:Password>", "<web:Password xsi:type=\"s\">[redacted]</web:Password>")]
    [InlineData("<sifre><![CDATA[hi<dden]]></sifre>", "<sifre>[redacted]</sifre>")]
    [InlineData("<SESSION_ID>abc</SESSION_ID>", "<SESSION_ID>[redacted]</SESSION_ID>")]
    [InlineData("<Password>abc...[truncated]", "<Password>[redacted]")]
    [InlineData("<Header password=\"hidden\" id=\"1\"/>", "<Header password=\"[redacted]\" id=\"1\"/>")]
    [InlineData("<Password/><Bank>Example</Bank>", "<Password/><Bank>Example</Bank>")]
    public void Redacts_Xml_Fields(string input, string expected)
    {
        SensitiveBodyRedactor.Redact(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("password=hidden&username=alice", "password=[redacted]&username=alice")]
    [InlineData("client_secret=abc", "client_secret=[redacted]")]
    public void Redacts_The_Leading_Form_Field(string input, string expected)
    {
        SensitiveBodyRedactor.Redact(input).ShouldBe(expected);
    }

    [Fact]
    public void Redacts_Every_Form_Field_When_The_Content_Type_Is_A_Form()
    {
        SensitiveBodyRedactor.Redact("grant_type=client_credentials&client_secret=abc&refresh_token=def", "application/x-www-form-urlencoded")
            .ShouldBe("grant_type=client_credentials&client_secret=[redacted]&refresh_token=[redacted]");
    }

    [Theory]
    [InlineData("Order sync was throttled, account: 01KNSCSM2F165RX7V665KNDXJ8")]
    [InlineData("{\"iban\":\"TR330006100519786457841326\",\"amount\":1500}")]
    [InlineData("<Bank>Example</Bank>")]
    public void Leaves_Other_Text_Unchanged(string input)
    {
        SensitiveBodyRedactor.Redact(input).ShouldBe(input);
        SensitiveBodyRedactor.Redact(input, "application/json").ShouldBe(input);
    }
}
