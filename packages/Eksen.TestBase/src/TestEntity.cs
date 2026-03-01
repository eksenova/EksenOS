using Eksen.Entities;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.TestBase;

public sealed record TestEntityId(System.Ulid Value) : UlidEntityId<TestEntityId>(Value);

public class TestEntity : IEntity<TestEntityId, System.Ulid>, ISoftDelete
{
    public TestEntityId Id { get; private init; }

    public TestEntityName Name { get; private set; }

    public bool IsDeleted { get; private init; }

    private TestEntity()
    {
        Id = TestEntityId.Empty;
        Name = null!;
    }

    public TestEntity(TestEntityName name) : this()
    {
        Id = TestEntityId.NewId();

        SetName(name);
    }

    private void SetName(TestEntityName name)
    {
        Name = name;
    }
}

public class TestEntityTypeConfiguration : IEntityTypeConfiguration<TestEntity>
{
    public void Configure(EntityTypeBuilder<TestEntity> builder)
    {
        builder.Property(x => x.Id)
            .HasConversion(
                v => v.Value.ToString(),
                v => TestEntityId.Parse(v))
            .HasMaxLength(TestEntityId.Length)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(e => e.Name)
            .HasConversion(
                v => v.Value,
                v => new TestEntityName(v))
            .HasMaxLength(TestEntityName.MaxLength)
            .IsRequired();

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}
