using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public static class AuditLogPropertyChangeTypeConfigurationExtensions
{
    extension(EntityTypeBuilder<AuditLogPropertyChange> builder)
    {
        public EntityTypeBuilder<AuditLogPropertyChange> ConfigureAuditLogPropertyChange()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogPropertyChangeId.Parse(value))
                .HasMaxLength(AuditLogPropertyChangeId.Length)
                .ValueGeneratedNever();

            builder.Property(x => x.EntityChangeId)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogEntityChangeId.Parse(value))
                .HasMaxLength(AuditLogEntityChangeId.Length)
                .IsRequired();

            builder.Property(x => x.PropertyName)
                .HasMaxLength(maxLength: 256)
                .IsRequired();

            builder.Property(x => x.PropertyTypeFullName)
                .HasMaxLength(maxLength: 512);

            builder.Property(x => x.OriginalValue);

            builder.Property(x => x.NewValue);

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.EntityChangeId);

            return builder;
        }
    }
}
