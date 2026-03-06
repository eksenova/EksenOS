using Microsoft.EntityFrameworkCore;

namespace Eksen.Auditing.EntityFrameworkCore;

public static class ModelBuilderExtensions
{
    public static void ApplyEksenAuditingConfiguration(this ModelBuilder builder)
    {
        builder.ApplyConfiguration(new AuditLogEntityTypeConfiguration());
        builder.ApplyConfiguration(new AuditLogHttpRequestEntityTypeConfiguration());
        builder.ApplyConfiguration(new AuditLogHttpRequestPayloadEntityTypeConfiguration());
        builder.ApplyConfiguration(new AuditLogActionEntityTypeConfiguration());
        builder.ApplyConfiguration(new AuditLogEntityChangeEntityTypeConfiguration());
        builder.ApplyConfiguration(new AuditLogPropertyChangeEntityTypeConfiguration());
    }
}
