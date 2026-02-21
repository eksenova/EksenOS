namespace Eksen.Emailing;

public interface ITemplateEmailSender
{
    Task SendTemplateEmailAsync<TTemplateModel>(
        SendTemplateEmailParameters<TTemplateModel> templateParameters,
        CancellationToken cancellationToken = default
    );
}