using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public static class RolePermissionTypeConfigurationExtensions
{
    extension<TRole, TTenant>(
        EntityTypeBuilder<EksenRolePermission<TRole, TTenant>> builder)
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        public EntityTypeBuilder<EksenRolePermission<TRole, TTenant>> ConfigureEksenRolePermission()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    v => v.Value,
                    v => new EksenRolePermissionId(v)
                )
                .HasMaxLength(EksenRolePermissionId.Length)
                .IsRequired();

            builder.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey("RoleId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.HasOne(x => x.PermissionDefinition)
                .WithMany()
                .HasForeignKey("PermissionDefinitionId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasKey(x => x.Id);

            var tableName = builder.Metadata.GetTableName();
            var tenantIdColumnName = builder.Metadata.FindNavigation(nameof(EksenUserPermission<,>.Tenant))
                ?.ForeignKey.Properties
                .FirstOrDefault()
                ?.GetColumnName();

            builder.HasIndex([
                        "RoleId",
                        "PermissionDefinitionId",
                        "TenantId"
                    ],
                    $"IX_{tableName}_RoleId_PermissionDefinitionId_TenantId")
                .HasFilter($"{tenantIdColumnName} IS NOT NULL")
                .IsUnique();

            builder.HasIndex([
                        "RoleId",
                        "PermissionDefinitionId"
                    ],
                    $"IX_{tableName}_RoleId_PermissionDefinitionId")
                .HasFilter($"{tenantIdColumnName} IS NULL")
                .IsUnique();

            return builder;
        }
    }
}