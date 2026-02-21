namespace Eksen.Repositories;

public record BaseQueryOptions
{
    public bool IgnoreQueryFilters { get; set; }

    public bool IgnoreAutoIncludes { get; set; }

    public bool AsNoTracking { get; set; }
}

public record DefaultQueryOptions : BaseQueryOptions;