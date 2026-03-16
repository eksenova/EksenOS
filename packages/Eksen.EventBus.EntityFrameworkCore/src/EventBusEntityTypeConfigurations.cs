using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.EventBus.EntityFrameworkCore;

public static class EventBusEntityTypeConfigurations
{
    public static ModelBuilder ApplyEventBusEntityConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessageEntity>(ConfigureOutboxMessage);
        modelBuilder.Entity<InboxMessageEntity>(ConfigureInboxMessage);
        modelBuilder.Entity<DeadLetterMessageEntity>(ConfigureDeadLetterMessage);
        return modelBuilder;
    }

    private static void ConfigureOutboxMessage(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("EventBusOutboxMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.CreationTime).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.SourceApp).HasMaxLength(256);
        builder.Property(x => x.TargetApp).HasMaxLength(256);
        builder.Property(x => x.LastError).HasMaxLength(2048);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreationTime);
        builder.HasIndex(x => new { x.Status, x.CreationTime });
    }

    private static void ConfigureInboxMessage(EntityTypeBuilder<InboxMessageEntity> builder)
    {
        builder.ToTable("EventBusInboxMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.EventId).IsRequired();
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(512);
        builder.Property(x => x.HandlerType).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.CreationTime).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.SourceApp).HasMaxLength(256);
        builder.Property(x => x.LastError).HasMaxLength(2048);

        builder.HasIndex(x => new { x.EventId, x.HandlerType }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreationTime);
    }

    private static void ConfigureDeadLetterMessage(EntityTypeBuilder<DeadLetterMessageEntity> builder)
    {
        builder.ToTable("EventBusDeadLetterMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.OriginalMessageId).IsRequired();
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(512);
        builder.Property(x => x.HandlerType).HasMaxLength(512);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.CreationTime).IsRequired();
        builder.Property(x => x.FailedTime).IsRequired();
        builder.Property(x => x.LastError).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.SourceApp).HasMaxLength(256);
        builder.Property(x => x.TargetApp).HasMaxLength(256);

        builder.HasIndex(x => x.OriginalMessageId);
        builder.HasIndex(x => x.FailedTime);
        builder.HasIndex(x => x.IsRequeued);
    }
}
