using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public static class UserPermissionTypeConfigurationExtensions
{
    extension<TUser, TTenant>(
        EntityTypeBuilder<EksenUserPermission<TUser, TTenant>> builder)
        where TUser : class, IEksenUser<TTenant>
        where TTenant : class, IEksenTenant
    {
        public EntityTypeBuilder<EksenUserPermission<TUser, TTenant>> ConfigureEksenUserPermission()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    v => v.Value,
                    v => new EksenUserPermissionId(v)
                )
                .HasMaxLength(EksenUserPermissionId.Length)
                .IsRequired();

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey("UserId")
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
                        "UserId",
                        "PermissionDefinitionId",
                        "TenantId"
                    ],
                    $"IX_{tableName}_UserId_PermissionDefinitionId_TenantId")
                .HasFilter($"{tenantIdColumnName} IS NOT NULL")
                .IsUnique();

            builder.HasIndex([
                        "UserId",
                        "PermissionDefinitionId"
                    ],
                    $"IX_{tableName}_UserId_PermissionDefinitionId")
                .HasFilter($"{tenantIdColumnName} IS NULL")
                .IsUnique();

            return builder;
        }
    }
}