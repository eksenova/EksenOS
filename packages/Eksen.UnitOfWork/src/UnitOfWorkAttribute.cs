using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Eksen.UnitOfWork;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Method)]
public sealed class UnitOfWorkAttribute : Attribute
{
    public bool IsEnabled { get; set; } = true;

    public IsolationLevel? IsolationLevel { get; set; }
}