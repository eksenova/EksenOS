using Eksen.Templating.Html;

namespace Eksen.Emailing;

internal sealed class TemplateEmailSender(
    IEmailSender emailSender,
    ITemplateHtmlRenderer templateHtmlRenderer
) : ITemplateEmailSender
{
    public async Task SendTemplateEmailAsync<TTemplateModel>(
        SendTemplateEmailParameters<TTemplateModel> templateParameters,
        CancellationToken cancellationToken = default)
    {
        var emailInstances = new List<EmailInstance>();

        foreach (var to in templateParameters.To)
        {
            var model = new EmailViewModel<TTemplateModel>
            {
                Data = templateParameters.Model,
                To = to
            };

            var templateKey = $"Emails/{templateParameters.TemplateKey}";

            var body = await templateHtmlRenderer.RenderTemplateAsync(
                templateKey,
                model,
                cancellationToken: cancellationToken
            );

            var subject = templateParameters.Subject;
            if (string.IsNullOrWhiteSpace(subject))
            {
                subject = model.Subject;

                if (string.IsNullOrWhiteSpace(subject))
                {
                    throw new ArgumentNullException(nameof(templateParameters.Subject));
                }
            }

            var emailInstance = new EmailInstance
            {
                ToAddress = to,
                Content = body,
                Subject = subject,
                FromName = model.FromName,
                FromAddress = model.FromAddress,
                ContentType = EmailContentType.Html
            };

            emailInstances.Add(emailInstance);
        }

        var emailParameters = new SendEmailParameters
        {
            To = emailInstances,
            FromAddress = templateParameters.FromAddress,
            FromName = templateParameters.FromName,
            Subject = templateParameters.Subject
        };

        await emailSender.SendEmailAsync(emailParameters, cancellationToken);
    }
}