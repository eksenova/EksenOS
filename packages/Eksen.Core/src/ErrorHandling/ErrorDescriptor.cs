using System.Runtime.CompilerServices;

namespace Eksen.Core.ErrorHandling;

public record ErrorDescriptor<TRaiseDelegate> : BaseErrorDescriptor
    where TRaiseDelegate : Delegate
{
    public ErrorDescriptor(
        string errorType,
        string codeCategory,
        Func<ErrorDescriptor<TRaiseDelegate>, TRaiseDelegate> raiseFunc,
        [CallerMemberName] string? memberName = null
    ) : base(errorType, codeCategory, memberName!)
    {
        ArgumentNullException.ThrowIfNull(raiseFunc);

        var raiseFuncInstance = raiseFunc(this);

        var returnType = raiseFuncInstance.Method.ReturnType;

        if (!typeof(ErrorInstance).IsAssignableFrom(returnType))
        {
            throw new ArgumentException($"The return type of the delegate must be assignable to {typeof(ErrorInstance).FullName}.", nameof(raiseFunc));
        }

        Raise = raiseFuncInstance;
    }

    public readonly TRaiseDelegate Raise;
}

public record ErrorDescriptor : BaseErrorDescriptor
{
    public ErrorDescriptor(
        string errorType,
        string codeCategory, 
        [CallerMemberName] string? memberName = null)
        : base(errorType, codeCategory, memberName!) { }

    public ErrorInstance Raise()
    {
        return new ErrorInstance(this);
    }
}