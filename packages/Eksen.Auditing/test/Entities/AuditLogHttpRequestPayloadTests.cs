using Eksen.Auditing.Entities;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests.Entities;

public class AuditLogHttpRequestPayloadTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_Payload_With_All_Parameters()
    {
        // Arrange
        var httpRequestId = AuditLogHttpRequestId.NewId();
        var requestBody = """{"orderNumber":"ORD-001","items":[]}""";
        var contentType = "application/json";
        var size = 42L;

        // Act
        var payload = new AuditLogHttpRequestPayload(httpRequestId, requestBody, contentType, size);

        // Assert
        payload.Id.ShouldNotBeNull();
        payload.HttpRequestId.ShouldBe(httpRequestId);
        payload.RequestBody.ShouldBe(requestBody);
        payload.ContentType.ShouldBe(contentType);
        payload.Size.ShouldBe(size);
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Values()
    {
        // Arrange & Act
        var payload = new AuditLogHttpRequestPayload(
            AuditLogHttpRequestId.NewId(), null, null, null);

        // Assert
        payload.RequestBody.ShouldBeNull();
        payload.ContentType.ShouldBeNull();
        payload.Size.ShouldBeNull();
    }
}
