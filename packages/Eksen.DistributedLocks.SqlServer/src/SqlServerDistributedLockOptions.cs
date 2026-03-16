using System.ComponentModel.DataAnnotations;

namespace Eksen.DistributedLocks.SqlServer;

public sealed record SqlServerDistributedLockOptions
{
    public const string DefaultConfigSectionPath = "Eksen:DistributedLocks:SqlServer";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
