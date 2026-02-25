using Eksen.Entities.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Identity.EntityFrameworkCore.Tenants;

public static class TenantTypeConfigurationExtensions
{
    extension<TTenant>(EntityTypeBuilder<TTenant> builder) where TTenant : class, IEksenTenant
    {
        public EntityTypeBuilder<TTenant> ConfigureEksenTenant()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    v => v.Value,
                    v => new EksenTenantId(v)
                )
                .HasMaxLength(EksenTenantId.Length)
                .IsRequired();

            builder.Property(x => x.Name)
                .HasConversion(
                    v => v.Value,
                    v => TenantName.Create(v)
                )
                .HasMaxLength(TenantName.MaxLength)
                .IsRequired();


            builder.HasKey(x => x.Id);

            var tableName = builder.Metadata.GetTableName();

            builder.HasIndex("Name", $"IX_{tableName}_Name")
                .IsUnique();

            return builder;
        }
    }
}