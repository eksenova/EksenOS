namespace Eksen.Repositories;

public record BaseSortingParameters;

public record DefaultSortingParameters : BaseSortingParameters
{
    public string? Sorting { get; set; }
}