using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public sealed class
    EksenPermissionDefinitionEntityTypeConfiguration : IEntityTypeConfiguration<PermissionDefinition>
{
    public void Configure(EntityTypeBuilder<PermissionDefinition> builder)
    {
        builder.ToTable(name: "PermissionDefinitions");
        builder.ConfigurePermissionDefinition();
    }
}