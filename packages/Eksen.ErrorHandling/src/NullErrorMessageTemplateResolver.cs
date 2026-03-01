namespace Eksen.ErrorHandling;

public sealed class NullErrorMessageTemplateResolver : IErrorMessageTemplateResolver
{
    public string ResolveErrorMessageTemplate(string code)
    {
        return code;
    }
}