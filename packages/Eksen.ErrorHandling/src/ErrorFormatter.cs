using Eksen.Localization.Formatting;

namespace Eksen.ErrorHandling;

public class ErrorFormatter(
    IErrorMessageTemplateResolver errorMessageTemplateResolver,
    IMessageFormatter formatter
) : IErrorFormatter
{
    public virtual string FormatError(IErrorData errorData)
    {
        var messageTemplate = errorMessageTemplateResolver.ResolveErrorMessageTemplate(errorData.Descriptor.Code);
        var parameters = new List<FormatParameter>();
        foreach (var (k, v) in errorData.Data)
        {
            parameters.Add(new FormatParameter(k, v));
        }

        return formatter.FormatMessage(messageTemplate, parameters);
    }
}