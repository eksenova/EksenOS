using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Eksen.Emailing;

internal sealed class SmtpEmailSender(
    IOptions<SmtpConfiguration> smtpConfiguration
) : IEmailSender
{
    private readonly SmtpConfiguration _smtpConfiguration = smtpConfiguration.Value;

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

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress.Value, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = emailInstance.ContentType == EmailContentType.Html
            };

            mailMessage.To.Add(new MailAddress(emailInstance.ToAddress.Value));
            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
        }
    }
}