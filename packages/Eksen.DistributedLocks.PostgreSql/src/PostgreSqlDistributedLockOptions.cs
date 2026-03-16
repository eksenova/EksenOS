using System.ComponentModel.DataAnnotations;

namespace Eksen.DistributedLocks.PostgreSql;

public sealed record PostgreSqlDistributedLockOptions
{
    public const string DefaultConfigSectionPath = "Eksen:DistributedLocks:PostgreSql";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
