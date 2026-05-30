using Eksen.TestBase;
using Eksen.TestBase.SqlServer;

namespace Eksen.EntityFrameworkCore.SqlServer.Tests;

public abstract class TestDbContextSqlServerTestBase(SqlServerWorkerPool pool)
    : EksenSqlServerTestBase<TestDbContext>(pool);
