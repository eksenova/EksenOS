using System.Net;
using Eksen.EventBus.Alerts;
using Eksen.EventBus.DeadLetter;
using Eksen.TestBase;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shouldly;

namespace Eksen.EventBus.Alerts.Slack.Tests;

public class SlackDeadLetterAlertChannelTests : EksenUnitTestBase
{
    private static DeadLetterAlert CreateAlert(string? lastError = null)
    {
        return new DeadLetterAlert
        {
            Message = new DeadLetterMessage
            {
                Id = Guid.NewGuid(),
                OriginalMessageId = Guid.NewGuid(),
                EventType = "OrderCreated",
                HandlerType = "OrderHandler",
                Payload = "{}",
                CreationTime = DateTime.UtcNow,
                FailedTime = DateTime.UtcNow,
                TotalRetryCount = 3,
                LastError = lastError ?? "Connection refused",
                CorrelationId = "corr-123",
                SourceApp = "OrderService"
            },
            AppName = "TestApp"
        };
    }

    private static (SlackDeadLetterAlertChannel channel, Mock<HttpMessageHandler> handler) CreateChannel(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        SlackAlertOptions? options = null)
    {
        var messageHandler = new Mock<HttpMessageHandler>();
        messageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("ok")
            });

        var httpClient = new HttpClient(messageHandler.Object);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("EksenEventBusSlack"))
            .Returns(httpClient);

        var slackOptions = options ?? new SlackAlertOptions
        {
            WebhookUrl = "https://hooks.slack.com/test",
            Channel = "#alerts",
            Username = "Test Bot"
        };

        var channel = new SlackDeadLetterAlertChannel(
            httpClientFactory.Object,
            Options.Create(slackOptions),
            NullLogger<SlackDeadLetterAlertChannel>.Instance);

        return (channel, messageHandler);
    }

    [Fact]
    public void Name_Should_Be_Slack()
    {
        // Arrange
        var (channel, _) = CreateChannel();

        // Assert
        channel.Name.ShouldBe("Slack");
    }

    [Fact]
    public async Task SendAlertAsync_Should_Post_To_Webhook()
    {
        // Arrange
        var (channel, handler) = CreateChannel();
        var alert = CreateAlert();

        // Act
        await channel.SendAlertAsync(alert);

        // Assert
        handler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://hooks.slack.com/test"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAlertAsync_Should_Include_Event_Details_In_Payload()
    {
        // Arrange
        string? requestBody = null;
        var messageHandler = new Mock<HttpMessageHandler>();
        messageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                requestBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(messageHandler.Object);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("EksenEventBusSlack")).Returns(httpClient);

        var channel = new SlackDeadLetterAlertChannel(
            httpClientFactory.Object,
            Options.Create(new SlackAlertOptions { WebhookUrl = "https://hooks.slack.com/test" }),
            NullLogger<SlackDeadLetterAlertChannel>.Instance);

        var alert = CreateAlert("Connection refused");

        // Act
        await channel.SendAlertAsync(alert);

        // Assert
        requestBody.ShouldNotBeNull();
        requestBody.ShouldContain("OrderCreated");
        requestBody.ShouldContain("OrderHandler");
        requestBody.ShouldContain("Connection refused");
        requestBody.ShouldContain("TestApp");
    }

    [Fact]
    public async Task SendAlertAsync_Should_Not_Throw_On_Error_Response()
    {
        // Arrange
        var (channel, _) = CreateChannel(HttpStatusCode.InternalServerError);
        var alert = CreateAlert();

        // Act & Assert (should not throw)
        await channel.SendAlertAsync(alert);
    }

    [Fact]
    public async Task SendAlertAsync_Should_Truncate_Long_Errors()
    {
        // Arrange
        string? requestBody = null;
        var messageHandler = new Mock<HttpMessageHandler>();
        messageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                requestBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = new HttpClient(messageHandler.Object);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("EksenEventBusSlack")).Returns(httpClient);

        var channel = new SlackDeadLetterAlertChannel(
            httpClientFactory.Object,
            Options.Create(new SlackAlertOptions { WebhookUrl = "https://hooks.slack.com/test" }),
            NullLogger<SlackDeadLetterAlertChannel>.Instance);

        var longError = new string('X', 1000);
        var alert = CreateAlert(longError);

        // Act
        await channel.SendAlertAsync(alert);

        // Assert
        requestBody.ShouldNotBeNull();
        // The truncated error should be at most 503 chars (500 + "...")
        requestBody.ShouldContain("...");
        requestBody.ShouldNotContain(longError);
    }
}
