using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public static class AuditLogHttpRequestTypeConfigurationExtensions
{
    extension(EntityTypeBuilder<AuditLogHttpRequest> builder)
    {
        public EntityTypeBuilder<AuditLogHttpRequest> ConfigureAuditLogHttpRequest()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogHttpRequestId.Parse(value))
                .HasMaxLength(AuditLogHttpRequestId.Length)
                .ValueGeneratedNever();

            builder.Property(x => x.AuditLogId)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogId.Parse(value))
                .HasMaxLength(AuditLogId.Length)
                .IsRequired();

            builder.Property(x => x.Method)
                .HasMaxLength(maxLength: 10)
                .IsRequired();

            builder.Property(x => x.Host)
                .HasMaxLength(maxLength: 256)
                .IsRequired();

            builder.Property(x => x.Path)
                .HasMaxLength(maxLength: 2048)
                .IsRequired();

            builder.Property(x => x.QueryString)
                .HasMaxLength(maxLength: 4096);

            builder.Property(x => x.Scheme)
                .HasMaxLength(maxLength: 10);

            builder.Property(x => x.Protocol)
                .HasMaxLength(maxLength: 20);

            builder.Property(x => x.UserAgent)
                .HasMaxLength(maxLength: 512);

            builder.Property(x => x.ContentType)
                .HasMaxLength(maxLength: 256);

            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Payload)
                .WithOne()
                .HasForeignKey<AuditLogHttpRequestPayload>(x => x.HttpRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.AuditLogId)
                .IsUnique();

            return builder;
        }
    }
}
