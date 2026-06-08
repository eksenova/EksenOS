using Eksen.Emailing;
using Eksen.Emailing.Gmail;
using Eksen.TestBase;
using MimeKit;
using Shouldly;

namespace Eksen.Emailing.Gmail.Tests;

public class GmailMessageFactoryTests : EksenUnitTestBase
{
    [Fact]
    public void CreateMimeMessage_Should_Set_From_Address_And_Name()
    {
        // Arrange & Act
        var mime = GmailMessageFactory.CreateMimeMessage(
            fromAddress: "sender@example.com",
            fromName: "Sender Name",
            toAddress: "recipient@example.com",
            subject: "Hello",
            body: "<p>Body</p>",
            contentType: EmailContentType.Html);

        // Assert
        var from = mime.From.Mailboxes.ShouldHaveSingleItem();
        from.Address.ShouldBe("sender@example.com");
        from.Name.ShouldBe("Sender Name");
    }

    [Fact]
    public void CreateMimeMessage_Should_Set_To_Address()
    {
        // Arrange & Act
        var mime = GmailMessageFactory.CreateMimeMessage(
            fromAddress: "sender@example.com",
            fromName: "Sender Name",
            toAddress: "recipient@example.com",
            subject: "Hello",
            body: "<p>Body</p>",
            contentType: EmailContentType.Html);

        // Assert
        var to = mime.To.Mailboxes.ShouldHaveSingleItem();
        to.Address.ShouldBe("recipient@example.com");
    }

    [Fact]
    public void CreateMimeMessage_Should_Set_Subject()
    {
        // Arrange & Act
        var mime = GmailMessageFactory.CreateMimeMessage(
            fromAddress: "sender@example.com",
            fromName: "Sender Name",
            toAddress: "recipient@example.com",
            subject: "My Subject",
            body: "<p>Body</p>",
            contentType: EmailContentType.Html);

        // Assert
        mime.Subject.ShouldBe("My Subject");
    }

    [Fact]
    public void CreateMimeMessage_Should_Set_Html_Body_For_Html_Content_Type()
    {
        // Arrange & Act
        var mime = GmailMessageFactory.CreateMimeMessage(
            fromAddress: "sender@example.com",
            fromName: "Sender Name",
            toAddress: "recipient@example.com",
            subject: "Subject",
            body: "<h1>Hi</h1>",
            contentType: EmailContentType.Html);

        // Assert
        var textPart = mime.Body.ShouldBeOfType<TextPart>();
        textPart.IsHtml.ShouldBeTrue();
        textPart.Text.ShouldBe("<h1>Hi</h1>");
    }

    [Fact]
    public void CreateMimeMessage_Should_Set_Plain_Body_For_Plaintext_Content_Type()
    {
        // Arrange & Act
        var mime = GmailMessageFactory.CreateMimeMessage(
            fromAddress: "sender@example.com",
            fromName: "Sender Name",
            toAddress: "recipient@example.com",
            subject: "Subject",
            body: "Plain text body",
            contentType: EmailContentType.Plaintext);

        // Assert
        var textPart = mime.Body.ShouldBeOfType<TextPart>();
        textPart.IsPlain.ShouldBeTrue();
        textPart.Text.ShouldBe("Plain text body");
    }

    [Fact]
    public void CreateRawMessage_Should_Produce_Non_Empty_Base64Url_Raw()
    {
        // Arrange & Act
        var message = GmailMessageFactory.CreateRawMessage(
            fromAddress: "sender@example.com",
            fromName: "Sender Name",
            toAddress: "recipient@example.com",
            subject: "Subject",
            body: "<p>Body</p>",
            contentType: EmailContentType.Html);

        // Assert
        message.Raw.ShouldNotBeNullOrWhiteSpace();
        message.Raw.ShouldNotContain("+");
        message.Raw.ShouldNotContain("/");
        message.Raw.ShouldNotContain("=");
    }

    [Fact]
    public void CreateRawMessage_Raw_Should_Decode_Back_To_The_Original_Message()
    {
        // Arrange
        var message = GmailMessageFactory.CreateRawMessage(
            fromAddress: "sender@example.com",
            fromName: "Sender Name",
            toAddress: "recipient@example.com",
            subject: "Round Trip",
            body: "<p>Body</p>",
            contentType: EmailContentType.Html);

        // Act
        var padded = message.Raw
            .Replace(oldChar: '-', newChar: '+')
            .Replace(oldChar: '_', newChar: '/');

        var remainder = padded.Length % 4;
        if (remainder != 0)
        {
            padded = padded.PadRight(padded.Length + (4 - remainder), '=');
        }

        var bytes = Convert.FromBase64String(padded);
        using var stream = new MemoryStream(bytes);
        var parsed = MimeMessage.Load(stream, TestContext.Current.CancellationToken);

        // Assert
        parsed.Subject.ShouldBe("Round Trip");
        parsed.From.Mailboxes.ShouldHaveSingleItem().Address.ShouldBe("sender@example.com");
        parsed.To.Mailboxes.ShouldHaveSingleItem().Address.ShouldBe("recipient@example.com");
    }
}
