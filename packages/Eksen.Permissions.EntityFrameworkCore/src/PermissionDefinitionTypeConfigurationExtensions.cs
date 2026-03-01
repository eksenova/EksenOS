using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public static class PermissionDefinitionTypeConfigurationExtensions
{
    extension(EntityTypeBuilder<PermissionDefinition> builder)
    {
        public EntityTypeBuilder<PermissionDefinition> ConfigurePermissionDefinition()
        {
            builder.Property(x => x.Id)
                .HasConversion(
                    id => id.Value,
                    value => new PermissionDefinitionId(value))
                .HasMaxLength(PermissionDefinitionId.Length)
                .ValueGeneratedNever();

            builder.Property(x => x.Name)
                .HasConversion(
                    name => name.Value,
                    value => PermissionName.Create(value))
                .HasMaxLength(PermissionName.MaxLength)
                .IsRequired();

            return builder;
        }
    }
}