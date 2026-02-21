namespace Eksen.Localization.ErrorHandling;

public interface IErrorMessageResolver
{
    string ResolveErrorMessage(string code);
}