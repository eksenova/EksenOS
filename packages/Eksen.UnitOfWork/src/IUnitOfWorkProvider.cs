using System.Data;

namespace Eksen.UnitOfWork;

public interface IUnitOfWorkProvider
{
    IUnitOfWorkProviderScope BeginScope(
        IUnitOfWorkScope parent,
        bool isTransctional,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default);
}