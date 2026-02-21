using System.Data;

namespace Eksen.UnitOfWork;

public interface IUnitOfWorkProvider
{
    Task<IUnitOfWorkProviderScope> BeginScopeAsync(
        IUnitOfWorkScope parent,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default);

    void PopScope(IUnitOfWorkProviderScope scope);
}