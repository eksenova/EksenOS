using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace Eksen.Emailing.Gmail;

internal sealed class GmailEmailSender(
    IOptions<GmailConfiguration> gmailConfiguration
) : IEmailSender
{
    private const string GmailSendUserId = "me";

    private readonly GmailConfiguration _gmailConfiguration = gmailConfiguration.Value;

    public async Task SendEmailAsync(
        SendEmailParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var impersonatedUser = _gmailConfiguration.ImpersonatedUser
                               ?? _gmailConfiguration.DefaultFromAddress;

        using var service = CreateGmailServiceWithImpersonation(
            _gmailConfiguration.ServiceAccountFile,
            impersonatedUser.Value,
            _gmailConfiguration.ApplicationName);

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
                fromName = _gmailConfiguration.DefaultFromName;
            }

            var fromAddress = emailInstance.FromAddress
                              ?? parameters.FromAddress
                              ?? _gmailConfiguration.DefaultFromAddress;

            var contentType = emailInstance.ContentType ?? EmailContentType.Html;

            var message = GmailMessageFactory.CreateRawMessage(
                fromAddress.Value,
                fromName,
                emailInstance.ToAddress.Value,
                subject,
                emailInstance.Content,
                contentType);

            await service.Users.Messages
                .Send(message, GmailSendUserId)
                .ExecuteAsync(cancellationToken);
        }
    }

    private static GmailService CreateGmailServiceWithImpersonation(
        string serviceAccountJsonPath,
        string userToImpersonateEmail,
        string applicationName)
    {
        var credential = GoogleCredential
            .FromFile(serviceAccountJsonPath)
            .CreateScoped(GmailService.Scope.GmailSend)
            .CreateWithUser(userToImpersonateEmail);

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = applicationName
        });
    }
}
