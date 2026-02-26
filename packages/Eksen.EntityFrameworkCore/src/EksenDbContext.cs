using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public abstract class EksenDbContext : DbContext
{
    protected EksenDbContext() { }

    protected EksenDbContext(DbContextOptions options) : base(options) { }
}