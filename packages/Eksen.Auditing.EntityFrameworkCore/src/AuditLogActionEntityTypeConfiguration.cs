using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Auditing.EntityFrameworkCore;

public sealed class AuditLogActionEntityTypeConfiguration : IEntityTypeConfiguration<AuditLogAction>
{
    public void Configure(EntityTypeBuilder<AuditLogAction> builder)
    {
        builder.ToTable(name: "AuditLogActions");
        builder.ConfigureAuditLogAction();
    }
}
