using Eksen.Auditing.Entities;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public static class AuditLogTypeConfigurationExtensions
{
    extension(EntityTypeBuilder<AuditLog> builder)
    {
        public EntityTypeBuilder<AuditLog> ConfigureAuditLog()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogId.Parse(value))
                .HasMaxLength(AuditLogId.Length)
                .ValueGeneratedNever();

            builder.Property(x => x.UserId)
                .HasConversion(
                    id => id != null ? id.Value.ToString() : null,
                    value => value != null ? EksenUserId.Parse(value) : null)
                .HasMaxLength(EksenUserId.Length);

            builder.Property(x => x.TenantId)
                .HasConversion(
                    id => id != null ? id.Value.ToString() : null,
                    value => value != null ? EksenTenantId.Parse(value) : null)
                .HasMaxLength(EksenTenantId.Length);

            builder.Property(x => x.LogTime)
                .IsRequired();

            builder.Property(x => x.SourceIpAddress)
                .HasMaxLength(maxLength: 45);

            builder.Property(x => x.CorrelationId)
                .HasMaxLength(maxLength: 64);

            builder.Property(x => x.ExceptionMessage)
                .HasMaxLength(maxLength: 2048);

            builder.Property(x => x.Metadata);

            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.HttpRequest)
                .WithOne()
                .HasForeignKey<AuditLogHttpRequest>(x => x.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Actions)
                .WithOne()
                .HasForeignKey(x => x.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.EntityChanges)
                .WithOne()
                .HasForeignKey(x => x.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.LogTime);
            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => x.TenantId);
            builder.HasIndex(x => x.CorrelationId);

            return builder;
        }
    }
}
