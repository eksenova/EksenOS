using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public sealed class AuditLogPropertyChangeEntityTypeConfiguration : IEntityTypeConfiguration<AuditLogPropertyChange>
{
    public void Configure(EntityTypeBuilder<AuditLogPropertyChange> builder)
    {
        builder.ToTable(name: "AuditLogPropertyChanges");
        builder.ConfigureAuditLogPropertyChange();
    }
}
