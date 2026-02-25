using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public static class UserRoleTypeConfigurationExtensions
{
    extension<TUser, TRole, TTenant>(
        EntityTypeBuilder<EksenUserRole<TUser, TRole, TTenant>> builder)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        public EntityTypeBuilder<EksenUserRole<TUser, TRole, TTenant>> ConfigureEksenUserRole()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    v => v.Value,
                    v => new EksenUserRoleId(v)
                )
                .HasMaxLength(EksenUserRoleId.Length)
                .IsRequired();

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey("RoleId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasKey(x => x.Id);

            var tableName = builder.Metadata.GetTableName();
            var tenantIdColumnName = builder.Metadata.FindNavigation(nameof(EksenUserRole<,,>.Tenant))
                ?.ForeignKey.Properties
                .FirstOrDefault()
                ?.GetColumnName();

            builder.HasIndex([
                        "UserId",
                        "RoleId",
                        "TenantId"
                    ],
                    $"IX_{tableName}_UserId_RoleId_TenantId")
                .HasFilter($"{tenantIdColumnName} IS NOT NULL")
                .IsUnique();

            builder.HasIndex([
                        "UserId",
                        "RoleId"
                    ],
                    $"IX_{tableName}_UserId_RoleId")
                .HasFilter($"{tenantIdColumnName} IS NULL")
                .IsUnique();

            return builder;
        }
    }
}