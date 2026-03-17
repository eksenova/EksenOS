using Eksen.Emailing;
using Eksen.Templating.Html;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Moq;
using Shouldly;

namespace Eksen.Emailing.Tests;

public class TemplateEmailSenderTests : EksenUnitTestBase
{
    [Fact]
    public async Task SendTemplateEmailAsync_Should_Render_Template_And_Send_Email()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var renderedHtml = "<h1>Hello</h1>";
        var templateKey = "Welcome";
        var subject = "Welcome Email";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(renderedHtml);

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = subject,
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "John" }
        };

        // Act
        await sut.SendTemplateEmailAsync(parameters);

        // Assert
        templateHtmlRenderer.Verify(
            r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        emailSender.Verify(
            s => s.SendEmailAsync(
                It.Is<SendEmailParameters>(p =>
                    p.To.Count == 1 &&
                    p.To.First().ToAddress == toAddress &&
                    p.To.First().Content == renderedHtml &&
                    p.To.First().Subject == subject &&
                    p.To.First().ContentType == EmailContentType.Html &&
                    p.Subject == subject),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Send_To_Multiple_Recipients()
    {
        // Arrange
        var to1 = EmailAddress.Create("user1@example.com");
        var to2 = EmailAddress.Create("user2@example.com");
        var renderedHtml = "<p>Body</p>";
        var subject = "Multi Email";
        var templateKey = "Notification";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(renderedHtml);

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [to1, to2],
            Subject = subject,
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "Team" }
        };

        // Act
        await sut.SendTemplateEmailAsync(parameters);

        // Assert
        templateHtmlRenderer.Verify(
            r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        emailSender.Verify(
            s => s.SendEmailAsync(
                It.Is<SendEmailParameters>(p =>
                    p.To.Count == 2 &&
                    p.To.Any(e => e.ToAddress == to1) &&
                    p.To.Any(e => e.ToAddress == to2)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Use_Model_Subject_When_Parameter_Subject_Is_Null()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var renderedHtml = "<p>Content</p>";
        var templateKey = "Reset";
        var modelSubject = "Reset Your Password";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, EmailViewModel<TestEmailModel>, dynamic?, CancellationToken>(
                (_, model, _, _) => model.Subject = modelSubject)
            .ReturnsAsync(renderedHtml);

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = null,
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "Jane" }
        };

        // Act
        await sut.SendTemplateEmailAsync(parameters);

        // Assert
        emailSender.Verify(
            s => s.SendEmailAsync(
                It.Is<SendEmailParameters>(p =>
                    p.To.First().Subject == modelSubject),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Throw_When_Subject_Is_Null_And_Model_Subject_Is_Null()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var templateKey = "NoSubject";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Body</p>");

        var emailSender = new Mock<IEmailSender>();

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = null,
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "Test" }
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendTemplateEmailAsync(parameters));

        emailSender.Verify(
            s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Throw_When_Subject_Is_Whitespace_And_Model_Subject_Is_Whitespace()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var templateKey = "BlankSubject";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<string, EmailViewModel<TestEmailModel>, dynamic?, CancellationToken>(
                (_, model, _, _) => model.Subject = "   ")
            .ReturnsAsync("<p>Body</p>");

        var emailSender = new Mock<IEmailSender>();

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = "   ",
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "Test" }
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendTemplateEmailAsync(parameters));

        emailSender.Verify(
            s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Pass_FromAddress_And_FromName()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var fromAddress = EmailAddress.Create("sender@example.com");
        var fromName = "Sender Name";
        var renderedHtml = "<p>Content</p>";
        var templateKey = "WithFrom";
        var subject = "Email Subject";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(renderedHtml);

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = subject,
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "User" },
            FromAddress = fromAddress,
            FromName = fromName
        };

        // Act
        await sut.SendTemplateEmailAsync(parameters);

        // Assert
        emailSender.Verify(
            s => s.SendEmailAsync(
                It.Is<SendEmailParameters>(p =>
                    p.FromAddress == fromAddress &&
                    p.FromName == fromName),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Prepend_Emails_Folder_To_TemplateKey()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var templateKey = "CustomTemplate";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                It.IsAny<string>(),
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Body</p>");

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = "Test",
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "User" }
        };

        // Act
        await sut.SendTemplateEmailAsync(parameters);

        // Assert
        templateHtmlRenderer.Verify(
            r => r.RenderTemplateAsync(
                "Emails/CustomTemplate",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Set_ContentType_To_Html()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var templateKey = "HtmlTemplate";

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.IsAny<EmailViewModel<TestEmailModel>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Html Content</p>");

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = "Subject",
            TemplateKey = templateKey,
            Model = new TestEmailModel { Name = "User" }
        };

        // Act
        await sut.SendTemplateEmailAsync(parameters);

        // Assert
        emailSender.Verify(
            s => s.SendEmailAsync(
                It.Is<SendEmailParameters>(p =>
                    p.To.All(e => e.ContentType == EmailContentType.Html)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTemplateEmailAsync_Should_Pass_Model_Data_To_Template_Renderer()
    {
        // Arrange
        var toAddress = EmailAddress.Create("recipient@example.com");
        var templateKey = "DataTemplate";
        var modelData = new TestEmailModel { Name = "DataTest" };

        var templateHtmlRenderer = new Mock<ITemplateHtmlRenderer>();
        templateHtmlRenderer
            .Setup(r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.Is<EmailViewModel<TestEmailModel>>(m =>
                    m.Data == modelData &&
                    m.To == toAddress),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Rendered</p>");

        var emailSender = new Mock<IEmailSender>();
        emailSender
            .Setup(s => s.SendEmailAsync(
                It.IsAny<SendEmailParameters>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new TemplateEmailSender(emailSender.Object, templateHtmlRenderer.Object);

        var parameters = new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [toAddress],
            Subject = "Subject",
            TemplateKey = templateKey,
            Model = modelData
        };

        // Act
        await sut.SendTemplateEmailAsync(parameters);

        // Assert
        templateHtmlRenderer.Verify(
            r => r.RenderTemplateAsync(
                $"Emails/{templateKey}",
                It.Is<EmailViewModel<TestEmailModel>>(m =>
                    m.Data == modelData &&
                    m.To == toAddress),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
