using System.Net;
using Eksen.TestBase;
using Eksen.Templating.Pdf;
using Shouldly;

namespace Eksen.Templating.Tests;

public class GotenbergHtmlPdfRendererTests : EksenUnitTestBase
{
    [Fact]
    public async Task ConvertAsync_Should_Return_Pdf_Bytes()
    {
        // Arrange
        var expectedPdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        var html = "<html><body><h1>Test</h1></body></html>";

        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedPdfBytes)
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };

        var renderer = new GotenbergHtmlPdfRenderer(httpClient);

        // Act
        var result = await renderer.ConvertAsync(html);

        // Assert
        result.ShouldBe(expectedPdfBytes);
    }

    [Fact]
    public async Task ConvertAsync_Should_Post_To_Correct_Endpoint()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x25, 0x50])
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };

        var renderer = new GotenbergHtmlPdfRenderer(httpClient);

        // Act
        await renderer.ConvertAsync("<html></html>");

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.RequestUri!.PathAndQuery
            .ShouldBe("/forms/chromium/convert/html");
    }

    [Fact]
    public async Task ConvertAsync_Should_Send_Multipart_Content()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x25])
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };

        var renderer = new GotenbergHtmlPdfRenderer(httpClient);

        // Act
        await renderer.ConvertAsync("<html><body>Test</body></html>");

        // Assert
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest!.Content.ShouldBeOfType<MultipartFormDataContent>();
    }

    [Fact]
    public async Task ConvertAsync_Should_Throw_When_Response_Is_Error()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000")
        };

        var renderer = new GotenbergHtmlPdfRenderer(httpClient);

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(
            () => renderer.ConvertAsync("<html></html>"));
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
