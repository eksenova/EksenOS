using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public sealed class AuditLogHttpRequestPayloadEntityTypeConfiguration
    : IEntityTypeConfiguration<AuditLogHttpRequestPayload>
{
    public void Configure(EntityTypeBuilder<AuditLogHttpRequestPayload> builder)
    {
        builder.ToTable(name: "AuditLogHttpRequestPayloads");
        builder.ConfigureAuditLogHttpRequestPayload();
    }
}
