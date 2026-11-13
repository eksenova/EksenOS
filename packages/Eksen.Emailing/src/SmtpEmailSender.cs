using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using Eksen.Telemetry;
using Microsoft.Extensions.Options;

namespace Eksen.Emailing;

internal sealed class SmtpEmailSender(
    IOptions<SmtpConfiguration> smtpConfiguration,
    EmailingTelemetry? telemetry = null
) : IEmailSender
{
    private const string SmtpStatusCodeTag = "eksen.smtp.status_code";

    private readonly SmtpConfiguration _smtpConfiguration = smtpConfiguration.Value;
    private readonly EmailingTelemetry _telemetry = telemetry ?? EmailingTelemetry.Shared;

    public async Task SendEmailAsync(
        SendEmailParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var smtpClient = new SmtpClient(_smtpConfiguration.Host, _smtpConfiguration.Port);
        smtpClient.Credentials = new NetworkCredential(
            _smtpConfiguration.UserName,
            _smtpConfiguration.Password);
        smtpClient.EnableSsl = _smtpConfiguration.EnableTls;
        smtpClient.UseDefaultCredentials = false;
        smtpClient.Timeout = 15000;

        foreach (var emailInstance in parameters.To)
        {
            var subject = emailInstance.Subject;
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = parameters.Subject;

                if (string.IsNullOrWhiteSpace(subject))
                {
                    throw new ArgumentNullException(nameof(parameters.Subject));
                }
            }

            var fromName = emailInstance.FromName;
            if (string.IsNullOrWhiteSpace(fromName))
            {
                fromName = parameters.FromName;
            }

            if (string.IsNullOrWhiteSpace(fromName))
            {
                fromName = _smtpConfiguration.DefaultFromName;
            }

            var fromAddress = emailInstance.FromAddress
                              ?? parameters.FromAddress
                              ?? _smtpConfiguration.DefaultFromAddress;

            var body = emailInstance.Content;

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress.Value, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = emailInstance.ContentType == EmailContentType.Html
            };

            if (emailInstance.MessageId is { } messageId)
            {
                if (messageId.Contains('\r') || messageId.Contains('\n'))
                    throw new ArgumentException("Message identity must contain one header value.", nameof(emailInstance.MessageId));
                mailMessage.Headers.Add("Message-ID", messageId);
            }

            mailMessage.To.Add(new MailAddress(emailInstance.ToAddress.Value));
            await SendWithTelemetryAsync(smtpClient, mailMessage, cancellationToken);
        }
    }

    private async Task SendWithTelemetryAsync(
        SmtpClient smtpClient,
        MailMessage mailMessage,
        CancellationToken cancellationToken)
    {
        // Only the server is recorded: recipients, senders and subjects are personal data.
        using var activity = EmailingTelemetry.ActivitySource.StartActivity(
            "smtp send",
            ActivityKind.Client,
            parentContext: default,
            tags:
            [
                new KeyValuePair<string, object?>(EksenSemanticConventions.ServerAddress, _smtpConfiguration.Host),
                new KeyValuePair<string, object?>(EksenSemanticConventions.ServerPort, _smtpConfiguration.Port)
            ]);

        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "failed";
        string? errorType = null;
        try
        {
            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            outcome = "sent";
        }
        catch (Exception exception)
        {
            errorType = exception.GetType().FullName ?? exception.GetType().Name;
            RecordFailure(activity, exception, errorType);
            throw;
        }
        finally
        {
            var tags = new TagList { { EmailingTelemetry.OutcomeTag, outcome } };
            if (errorType is not null)
            {
                tags.Add(EksenSemanticConventions.ErrorType, errorType);
            }

            _telemetry.Sent.Add(1, tags);
            _telemetry.SendDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, tags);
        }
    }

    /// <summary>
    /// Marks the span failed without the exception's text. SMTP servers echo the rejected mailbox in their replies
    /// (<c>550 5.1.1 &lt;person@example.com&gt;: Recipient address rejected</c>), and .NET puts that reply in the
    /// message of <see cref="SmtpFailedRecipientException"/> and its inner exceptions, so the message, the full
    /// <c>ToString()</c> and the status description could all carry a recipient. The span gets the exception
    /// type, the SMTP status code and the stack frames only.
    /// </summary>
    private static void RecordFailure(Activity? activity, Exception exception, string errorType)
    {
        if (activity is null)
        {
            return;
        }

        var statusCode = exception is SmtpException smtpException ? smtpException.StatusCode.ToString() : null;
        var description = statusCode is null ? $"SMTP send failed: {errorType}" : $"SMTP send failed: {statusCode}";

        var tags = new TagList
        {
            { EksenSemanticConventions.ExceptionType, errorType },
            { EksenSemanticConventions.ExceptionMessage, description }
        };
        if (exception.StackTrace is { } stackTrace)
        {
            tags.Add(EksenSemanticConventions.ExceptionStacktrace, $"{errorType}{Environment.NewLine}{stackTrace}");
        }

        activity.AddException(exception, tags);
        if (statusCode is not null)
        {
            activity.SetTag(SmtpStatusCodeTag, statusCode);
        }

        activity.SetTag(EksenSemanticConventions.ErrorType, errorType);
        activity.SetStatus(ActivityStatusCode.Error, description);
    }
}
