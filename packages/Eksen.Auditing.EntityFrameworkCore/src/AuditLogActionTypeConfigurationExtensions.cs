using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public static class AuditLogActionTypeConfigurationExtensions
{
    extension(EntityTypeBuilder<AuditLogAction> builder)
    {
        public EntityTypeBuilder<AuditLogAction> ConfigureAuditLogAction()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogActionId.Parse(value))
                .HasMaxLength(AuditLogActionId.Length)
                .ValueGeneratedNever();

            builder.Property(x => x.AuditLogId)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogId.Parse(value))
                .HasMaxLength(AuditLogId.Length)
                .IsRequired();

            builder.Property(x => x.LogTime)
                .IsRequired();

            builder.Property(x => x.ServiceType)
                .HasMaxLength(maxLength: 512)
                .IsRequired();

            builder.Property(x => x.MethodName)
                .HasMaxLength(maxLength: 256)
                .IsRequired();

            builder.Property(x => x.Parameters);

            builder.Property(x => x.ReturnValue);

            builder.Property(x => x.ExceptionMessage)
                .HasMaxLength(maxLength: 2048);

            builder.Property(x => x.Metadata);

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.AuditLogId);
            builder.HasIndex(x => x.ServiceType);

            return builder;
        }
    }
}