using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Authentication.ApiKeys.Identity.EntityFrameworkCore;

public static class UserApiKeyTypeConfigurationExtensions
{
    extension<TUser, TTenant>(
        EntityTypeBuilder<EksenUserApiKey<TUser, TTenant>> builder)
        where TUser : class, IEksenUser<TTenant>
        where TTenant : class, IEksenTenant
    {
        public EntityTypeBuilder<EksenUserApiKey<TUser, TTenant>> ConfigureEksenUserApiKey()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    v => v.Value.ToString(),
                    v => EksenUserApiKeyId.Parse(v)
                )
                .HasMaxLength(EksenUserApiKeyId.Length)
                .IsRequired();

            builder.Property(x => x.Name)
                .HasConversion(
                    v => v.Value,
                    v => ApiKeyName.Create(v))
                .HasMaxLength(ApiKeyName.MaxLength)
                .IsRequired();

            builder.Property(x => x.KeyValue)
                .HasConversion(
                    v => v.Value,
                    v => ApiKeyValue.Create(v))
                .HasMaxLength(ApiKeyValue.MaxLength)
                .IsRequired();

            builder.Property(x => x.ExpiresAt);
            builder.Property(x => x.RevokedAt);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey("TenantId")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.IsRevoked);
            builder.Ignore(x => x.IsExpired);
            builder.Ignore(x => x.IsActive);

            var tableName = builder.Metadata.GetTableName();
            var tenantIdColumnName = builder.Metadata.FindNavigation(nameof(EksenUserApiKey<TUser, TTenant>.Tenant))
                ?.ForeignKey.Properties
                .FirstOrDefault()
                ?.GetColumnName();

            builder.HasIndex(["UserId", "Name", "TenantId"],
                    $"IX_{tableName}_UserId_Name_TenantId")
                .HasFilter($"{tenantIdColumnName} IS NOT NULL")
                .IsUnique();

            builder.HasIndex(["UserId", "Name"],
                    $"IX_{tableName}_UserId_Name")
                .HasFilter($"{tenantIdColumnName} IS NULL")
                .IsUnique();

            builder.HasIndex(x => x.KeyValue)
                .IsUnique();

            return builder;
        }
    }
}
