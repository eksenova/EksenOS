# Eksen.TestBase.SqlServer

xUnit v3 integration-test base for SQL Server, backed by an assembly-shared, bounded pool of
Testcontainers SQL Server instances. Tests across parallel collections each acquire an idle worker
and release it on completion, so a small set of containers serves the whole assembly.

## Consumer setup

### 1. Register the shared worker pool (once per test assembly)

The pool is an xUnit v3 assembly fixture. Declare it once anywhere in the test assembly
(for example in a file named `AssemblyFixtures.cs`):

```csharp
using Eksen.TestBase.SqlServer;
using Xunit;

[assembly: AssemblyFixture(typeof(SqlServerWorkerPool))]
```

xUnit v3 creates exactly one `SqlServerWorkerPool` for the assembly and injects it into the
constructor of any test class that requests it.

### 2. Derive your test base from `EksenSqlServerTestBase<TDbContext>`

```csharp
public abstract class MyDbTestBase(SqlServerWorkerPool pool)
    : EksenSqlServerTestBase<MyDbContext>(pool);
```

Concrete test classes inherit the pool parameter through their constructor:

```csharp
public sealed class WidgetRepositoryTests(SqlServerWorkerPool pool) : MyDbTestBase(pool)
{
    [Fact]
    public async Task Persists_widget()
    {
        var repository = ServiceProvider.GetRequiredService<IWidgetRepository>();
        // ...
    }
}
```

### 3. Enable parallel idle-worker distribution

The point of the shared pool is that idle workers are picked up by the next waiting test. That
only happens when xUnit runs test collections in parallel. xUnit v3 parallelizes collections within
an assembly by default; cap the degree of parallelism to your worker count via `xunit.runner.json`
in the test project (copied to the output directory):

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 5
}
```

```xml
<ItemGroup>
  <None Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Equivalently, configure it via an assembly attribute:

```csharp
[assembly: CollectionBehavior(MaxParallelThreads = 5)]
```

Put each integration test class in its own collection (or leave classes uncollected, which gives
each class its own implicit collection) so they run in parallel and contend for the pool.

## Configuration

| Environment variable   | Default | Notes                                                                                       |
| ---------------------- | ------- | ------------------------------------------------------------------------------------------- |
| `EKSEN_SQL_MAX_WORKERS` | 5       | Pool size. Hard-capped at 5 (`Math.Min`).                                                    |
| `EKSEN_SQL_CPUSET`      | `0-3`   | Logical CPU set pinned on each container. SQL Server 2025 asserts on an odd CPU count, so keep this an even set. |

Keep `maxParallelThreads` (or `CollectionBehavior.MaxParallelThreads`) less than or equal to
`EKSEN_SQL_MAX_WORKERS`; threads beyond the worker count simply block in `AcquireAsync` until a
worker is released.
