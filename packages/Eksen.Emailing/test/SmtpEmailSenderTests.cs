using Eksen.Emailing;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Emailing.Tests;

public class SmtpEmailSenderTests : EksenUnitTestBase
{
    private static SmtpConfiguration CreateDefaultSmtpConfiguration()
    {
        return new SmtpConfiguration
        {
            DefaultFromAddress = EmailAddress.Create("default@example.com"),
            DefaultFromName = "Default Sender",
            Host = "smtp.example.com",
            Port = 587,
            UserName = "user",
            Password = "pass",
            EnableTls = true
        };
    }

    private static SmtpEmailSender CreateSut(SmtpConfiguration? config = null)
    {
        config ??= CreateDefaultSmtpConfiguration();
        var options = new Mock<IOptions<SmtpConfiguration>>();
        options.Setup(o => o.Value).Returns(config);
        return new SmtpEmailSender(options.Object);
    }

    [Fact]
    public async Task SendEmailAsync_Should_Throw_When_Instance_Subject_And_Parameter_Subject_Are_Both_Null()
    {
        // Arrange
        var sut = CreateSut();

        var parameters = new SendEmailParameters
        {
            To =
            [
                new EmailInstance
                {
                    ToAddress = EmailAddress.Create("recipient@example.com"),
                    Content = "<p>Body</p>",
                    Subject = null
                }
            ],
            Subject = null
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendEmailAsync(parameters));
    }

    [Fact]
    public async Task SendEmailAsync_Should_Throw_When_Instance_Subject_And_Parameter_Subject_Are_Both_Whitespace()
    {
        // Arrange
        var sut = CreateSut();

        var parameters = new SendEmailParameters
        {
            To =
            [
                new EmailInstance
                {
                    ToAddress = EmailAddress.Create("recipient@example.com"),
                    Content = "<p>Body</p>",
                    Subject = "   "
                }
            ],
            Subject = "   "
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendEmailAsync(parameters));
    }

    [Fact]
    public async Task SendEmailAsync_Should_Throw_When_Instance_Subject_Is_Empty_And_Parameter_Subject_Is_Empty()
    {
        // Arrange
        var sut = CreateSut();

        var parameters = new SendEmailParameters
        {
            To =
            [
                new EmailInstance
                {
                    ToAddress = EmailAddress.Create("recipient@example.com"),
                    Content = "<p>Body</p>",
                    Subject = ""
                }
            ],
            Subject = ""
        };

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendEmailAsync(parameters));
    }
}
