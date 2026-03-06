using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public sealed class AuditLogEntityChangeEntityTypeConfiguration : IEntityTypeConfiguration<AuditLogEntityChange>
{
    public void Configure(EntityTypeBuilder<AuditLogEntityChange> builder)
    {
        builder.ToTable(name: "AuditLogEntityChanges");
        builder.ConfigureAuditLogEntityChange();
    }
}
