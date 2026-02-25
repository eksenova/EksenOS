using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Identity.EntityFrameworkCore.Roles;

public static class RoleTypeConfigurationExtensions
{
    extension<TRole, TTenant>(
        EntityTypeBuilder<TRole> builder)
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        public EntityTypeBuilder<TRole> ConfigureEksenRole()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    v => v.Value,
                    v => new EksenRoleId(v)
                )
                .HasMaxLength(EksenRoleId.Length)
                .IsRequired();

            builder.Property(x => x.Name)
                .HasConversion(
                    v => v.Value,
                    v => RoleName.Create(v)
                )
                .HasMaxLength(RoleName.MaxLength)
                .IsRequired();

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasKey(x => x.Id);

            var tableName = builder.Metadata.GetTableName();
            var tenantIdColumnName = builder.Metadata.FindNavigation(nameof(IEksenRole<>.Tenant))
                ?
                .ForeignKey.Properties
                .FirstOrDefault()
                ?
                .GetColumnName();

            builder.HasIndex([
                        "Name",
                        "TenantId"
                    ],
                    $"IX_{tableName}_Name_TenantId")
                .HasFilter($"{tenantIdColumnName} IS NOT NULL")
                .IsUnique();

            builder.HasIndex("Name",
                    $"IX_{tableName}_Name")
                .HasFilter($"{tenantIdColumnName} IS NULL")
                .IsUnique();

            return builder;
        }
    }
}