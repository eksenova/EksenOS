namespace Eksen.Emailing;

public interface IEmailSender
{
    Task SendEmailAsync(
        SendEmailParameters parameters,
        CancellationToken cancellationToken = default
    );
}