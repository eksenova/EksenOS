using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public static class AuditLogEntityChangeTypeConfigurationExtensions
{
    extension(EntityTypeBuilder<AuditLogEntityChange> builder)
    {
        public EntityTypeBuilder<AuditLogEntityChange> ConfigureAuditLogEntityChange()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogEntityChangeId.Parse(value))
                .HasMaxLength(AuditLogEntityChangeId.Length)
                .ValueGeneratedNever();

            builder.Property(x => x.AuditLogId)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogId.Parse(value))
                .HasMaxLength(AuditLogId.Length)
                .IsRequired();

            builder.Property(x => x.ChangeTime)
                .IsRequired();

            builder.Property(x => x.ChangeType)
                .IsRequired();

            builder.Property(x => x.EntityTypeFullName)
                .HasMaxLength(maxLength: 512)
                .IsRequired();

            builder.Property(x => x.EntityId)
                .HasMaxLength(maxLength: 128);

            builder.HasKey(x => x.Id);

            builder.HasMany(x => x.PropertyChanges)
                .WithOne()
                .HasForeignKey(x => x.EntityChangeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.AuditLogId);
            builder.HasIndex(x => x.EntityTypeFullName);
            builder.HasIndex(x => new { x.EntityTypeFullName, x.EntityId });

            return builder;
        }
    }
}
