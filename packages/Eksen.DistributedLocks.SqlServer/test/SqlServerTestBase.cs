using Eksen.TestBase;
using Eksen.TestBase.SqlServer;

namespace Eksen.DistributedLocks.SqlServer.Tests;

public abstract class SqlServerTestBase(SqlServerWorkerPool pool)
    : EksenSqlServerTestBase<TestDbContext>(pool);
