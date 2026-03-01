namespace Eksen.ErrorHandling;

public interface IErrorMessageTemplateResolver
{
    string ResolveErrorMessageTemplate(string code);
}