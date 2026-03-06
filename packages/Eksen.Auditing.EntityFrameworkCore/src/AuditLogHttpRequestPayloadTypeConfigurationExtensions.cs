using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public static class AuditLogHttpRequestPayloadTypeConfigurationExtensions
{
    extension(EntityTypeBuilder<AuditLogHttpRequestPayload> builder)
    {
        public EntityTypeBuilder<AuditLogHttpRequestPayload> ConfigureAuditLogHttpRequestPayload()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogHttpRequestPayloadId.Parse(value))
                .HasMaxLength(AuditLogHttpRequestPayloadId.Length)
                .ValueGeneratedNever();

            builder.Property(x => x.HttpRequestId)
                .HasConversion(
                    id => id.Value.ToString(),
                    value => AuditLogHttpRequestId.Parse(value))
                .HasMaxLength(AuditLogHttpRequestId.Length)
                .IsRequired();

            builder.Property(x => x.ContentType)
                .HasMaxLength(maxLength: 256);

            builder.Property(x => x.RequestBody);

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.HttpRequestId)
                .IsUnique();

            return builder;
        }
    }
}
