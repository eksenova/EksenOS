namespace Eksen.Entities;

public interface IHasModificationTime
{
    DateTime? LastModificationTime { get; }
}