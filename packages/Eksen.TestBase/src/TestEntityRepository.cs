using Eksen.EntityFrameworkCore;
using Eksen.Repositories;

namespace Eksen.TestBase;

public interface ITestEntityRepository : IIdRepository<TestEntity, TestEntityId, System.Ulid>;

public class TestEntityRepository(TestDbContext dbContext)
    : EfCoreIdRepository<TestDbContext, TestEntity, TestEntityId, System.Ulid>(dbContext),
        ITestEntityRepository;