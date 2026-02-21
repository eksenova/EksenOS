namespace Eksen.Entities;

public interface ISoftDelete
{
    bool IsDeleted { get; }
}