namespace Eksen.Repositories;

public record BasePaginationParameters
{
    public int? SkipCount { get; set; }

    public int? MaxResultCount { get; set; }
}

public record DefaultPaginationParameters : BasePaginationParameters;