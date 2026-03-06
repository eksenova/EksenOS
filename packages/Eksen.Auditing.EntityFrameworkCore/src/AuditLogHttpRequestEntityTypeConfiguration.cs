using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public sealed class AuditLogHttpRequestEntityTypeConfiguration : IEntityTypeConfiguration<AuditLogHttpRequest>
{
    public void Configure(EntityTypeBuilder<AuditLogHttpRequest> builder)
    {
        builder.ToTable(name: "AuditLogHttpRequests");
        builder.ConfigureAuditLogHttpRequest();
    }
}
