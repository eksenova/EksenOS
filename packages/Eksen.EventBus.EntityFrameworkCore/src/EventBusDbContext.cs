using Eksen.EntityFrameworkCore;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EventBus.EntityFrameworkCore;

public class EventBusDbContext : EksenDbContext
{
    public EventBusDbContext(DbContextOptions<EventBusDbContext> options) : base(options) { }

    protected EventBusDbContext(DbContextOptions options) : base(options) { }

    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    public DbSet<InboxMessageEntity> InboxMessages => Set<InboxMessageEntity>();

    public DbSet<DeadLetterMessageEntity> DeadLetterMessages => Set<DeadLetterMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyEventBusEntityConfigurations();
    }
}
