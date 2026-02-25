namespace Eksen.Repositories;

public record BaseIncludeOptions<TEntity>
{
    public bool IgnoreAutoIncludes { get; set; }
}