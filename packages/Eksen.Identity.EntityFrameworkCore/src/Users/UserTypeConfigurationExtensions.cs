using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Identity.EntityFrameworkCore.Users;

public static class UserTypeConfigurationExtensions
{
    extension<TUser, TTenant>(
        EntityTypeBuilder<TUser> builder)
        where TUser : class, IEksenUser<TTenant>
        where TTenant : class, IEksenTenant
    {
        public EntityTypeBuilder<TUser> ConfigureEksenUser()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    v => v.Value,
                    v => new EksenUserId(v)
                )
                .HasMaxLength(EksenUserId.Length)
                .IsRequired();

            builder.Property(x => x.EmailAddress)
                .HasConversion(
                    v => v != null
                        ? v.Value
                        : null,
                    v => v != null
                        ? EmailAddress.Create(v)
                        : null
                )
                .HasMaxLength(EmailAddress.MaxLength);

            builder.Property(x => x.PasswordHash)
                .HasConversion(
                    v => v != null
                        ? v.Value
                        : null,
                    v => v != null
                        ? PasswordHash.Create(v)
                        : null
                )
                .HasDefaultValue(value: null)
                .HasMaxLength(PasswordHash.MaxLength);


            builder.HasKey(x => x.Id);

            var tableName = builder.Metadata.GetTableName();
            var tenantIdColumnName = builder.Metadata.FindNavigation(nameof(IEksenUser<>.Tenant))
                ?.ForeignKey.Properties
                .FirstOrDefault()
                ?.GetColumnName();

            builder.HasIndex([
                        "EmailAddress",
                        "TenantId"
                    ],
                    $"IX_{tableName}_EmailAddress_TenantId")
                .HasFilter($"{tenantIdColumnName} IS NOT NULL")
                .IsUnique();

            builder.HasIndex("Name",
                    $"IX_{tableName}_EmailAddress")
                .HasFilter($"{tenantIdColumnName} IS NULL")
                .IsUnique();

            return builder;
        }
    }
}