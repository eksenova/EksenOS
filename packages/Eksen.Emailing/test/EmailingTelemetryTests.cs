using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Mail;
using Eksen.Telemetry;
using Eksen.Templating.Html;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Emailing.Tests;

public class EmailingTelemetryTests : EksenUnitTestBase
{
    private static readonly ActivitySource TestSource = new("Eksen.Emailing.Tests");

    [Fact]
    public async Task Smtp_Send_Span_Carries_Only_The_Server()
    {
        var meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        using var collector = new TelemetryCollector(EksenTelemetry.Emailing, meterFactory);
        await using var server = new Fakes.LocalSmtpServer();
        var sender = CreateSender("127.0.0.1", server.Port, meterFactory);

        using var request = TestSource.StartActivity("request")!;
        await sender.SendEmailAsync(new SendEmailParameters
        {
            Subject = "Secret subject",
            To = [new EmailInstance { ToAddress = EmailAddress.Create("person@example.test"), Content = "body" }]
        }, TestContext.Current.CancellationToken);

        var span = collector.ActivitiesIn(request.TraceId).Single(activity => activity.DisplayName == "smtp send");
        span.Kind.ShouldBe(ActivityKind.Client);
        span.ParentSpanId.ShouldBe(request.SpanId);
        span.GetTagItem("server.address").ShouldBe("127.0.0.1");
        span.GetTagItem("server.port").ShouldBe(server.Port);
        span.Tags.ShouldNotContain(tag => tag.Value != null && (tag.Value.Contains("person@example.test") || tag.Value.Contains("Secret subject")));
        span.TagObjects.Select(tag => tag.Key).ShouldBe(["server.address", "server.port"], ignoreOrder: true);

        collector.MeasurementsOf("eksen.email.sent").ShouldHaveSingleItem().Tag("outcome").ShouldBe("sent");
        collector.MeasurementsOf("eksen.email.send.duration").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Failed_Send_Is_Counted_And_Marks_The_Span_Failed()
    {
        var meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        using var collector = new TelemetryCollector(EksenTelemetry.Emailing, meterFactory);
        int closedPort;
        using (var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0))
        {
            probe.Start();
            closedPort = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        }

        var sender = CreateSender("127.0.0.1", closedPort, meterFactory);

        using var request = TestSource.StartActivity("request")!;
        await Should.ThrowAsync<SmtpException>(() => sender.SendEmailAsync(new SendEmailParameters
        {
            Subject = "Subject",
            To = [new EmailInstance { ToAddress = EmailAddress.Create("person@example.test"), Content = "body" }]
        }, TestContext.Current.CancellationToken));

        collector.ActivitiesIn(request.TraceId).Single(activity => activity.DisplayName == "smtp send")
            .Status.ShouldBe(ActivityStatusCode.Error);
        var sent = collector.MeasurementsOf("eksen.email.sent").ShouldHaveSingleItem();
        sent.Tag("outcome").ShouldBe("failed");
        sent.Tag("error.type").ShouldBe(typeof(SmtpException).FullName);
    }

    [Fact]
    public async Task Rejected_Recipient_Leaves_No_Address_On_The_Span()
    {
        var meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
        using var collector = new TelemetryCollector(EksenTelemetry.Emailing, meterFactory);
        await using var server = new Fakes.LocalSmtpServer(
            "550 5.1.1 <person@example.test>: Recipient address rejected: User unknown");
        var sender = CreateSender("127.0.0.1", server.Port, meterFactory);

        using var request = TestSource.StartActivity("request")!;
        var exception = await Should.ThrowAsync<SmtpFailedRecipientException>(() => sender.SendEmailAsync(new SendEmailParameters
        {
            Subject = "Subject",
            To = [new EmailInstance { ToAddress = EmailAddress.Create("person@example.test"), Content = "body" }]
        }, TestContext.Current.CancellationToken));
        // The exception itself carries the address, which is why the span must not copy its text.
        exception.ToString().ShouldContain("person@example.test");

        var span = collector.ActivitiesIn(request.TraceId).Single(activity => activity.DisplayName == "smtp send");
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.StatusDescription.ShouldBe("SMTP send failed: MailboxUnavailable");
        span.GetTagItem("eksen.smtp.status_code").ShouldBe("MailboxUnavailable");
        span.GetTagItem("error.type").ShouldBe(typeof(SmtpFailedRecipientException).FullName);
        span.TagObjects.ShouldNotContain(tag => tag.Value != null && tag.Value.ToString()!.Contains('@'));
        span.StatusDescription!.ShouldNotContain("@");

        var exceptionEvent = span.Events.ShouldHaveSingleItem();
        exceptionEvent.Name.ShouldBe("exception");
        exceptionEvent.Tags.ShouldNotContain(tag => tag.Value != null && tag.Value.ToString()!.Contains('@'));
        exceptionEvent.Tags.Single(tag => tag.Key == "exception.type").Value
            .ShouldBe(typeof(SmtpFailedRecipientException).FullName);
        exceptionEvent.Tags.Single(tag => tag.Key == "exception.message").Value
            .ShouldBe("SMTP send failed: MailboxUnavailable");
    }

    [Fact]
    public async Task Template_Render_Has_Its_Own_Span()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.Emailing);
        var renderer = new Mock<ITemplateHtmlRenderer>();
        renderer.Setup(r => r.RenderTemplateAsync(
                It.IsAny<string>(), It.IsAny<EmailViewModel<TestEmailModel>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>hi</p>");
        var sut = new TemplateEmailSender(new Mock<IEmailSender>().Object, renderer.Object);

        using var request = TestSource.StartActivity("request")!;
        await sut.SendTemplateEmailAsync(new SendTemplateEmailParameters<TestEmailModel>
        {
            To = [EmailAddress.Create("person@example.test")],
            Subject = "Welcome",
            TemplateKey = "Welcome",
            Model = new TestEmailModel { Name = "John" }
        }, TestContext.Current.CancellationToken);

        var span = collector.ActivitiesIn(request.TraceId).Single(activity => activity.DisplayName == "email template render");
        span.GetTagItem("eksen.email.template").ShouldBe("Welcome");
    }

    private static SmtpEmailSender CreateSender(string host, int port, IMeterFactory meterFactory)
    {
        var configuration = new SmtpConfiguration
        {
            DefaultFromAddress = EmailAddress.Create("default@example.com"),
            DefaultFromName = "Default Sender",
            Host = host,
            Port = port,
            UserName = "user",
            Password = "pass",
            EnableTls = false
        };

        return new SmtpEmailSender(Options.Create(configuration), EmailingTelemetry.For(meterFactory));
    }
}
