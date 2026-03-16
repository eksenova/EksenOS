namespace Eksen.DistributedTransactions;

public class DistributedUnitOfWorkOptions
{
    public TimeSpan PostCommitTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
