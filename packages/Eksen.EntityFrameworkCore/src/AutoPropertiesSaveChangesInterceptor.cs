using Eksen.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eksen.EntityFrameworkCore;

public sealed class AutoPropertiesSaveChangesInterceptor : ISaveChangesInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var entries = eventData.Context?.ChangeTracker.Entries() ?? [];
        SaveChanges(entries);
        return result;
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var entries = eventData.Context?.ChangeTracker.Entries() ?? [];
        SaveChanges(entries);
        return result;
    }

    public void SaveChangesFailed(DbContextErrorEventData eventData) { }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var entries = eventData.Context?.ChangeTracker.Entries() ?? [];
        SaveChanges(entries);
        return ValueTask.FromResult(result);
    }

    public ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var entries = eventData.Context?.ChangeTracker.Entries() ?? [];
        SaveChanges(entries);

        return ValueTask.FromResult(result);
    }

    public Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private void SaveChanges(IEnumerable<EntityEntry> entries)
    {
        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType();

            switch (entry.State)
            {
                case EntityState.Deleted:
                {
                    if (entityType.IsAssignableTo(typeof(ISoftDelete)))
                    {
                        entry.State = EntityState.Modified;

                        foreach (var reference in entry.References)
                        {
                            if (reference.TargetEntry != null)
                            {
                                reference.TargetEntry.State = EntityState.Modified;
                            }
                        }

                        entityType.GetProperty(name: "IsDeleted")?.SetValue(entry.Entity, value: true);
                    }

                    break;
                }

                case EntityState.Modified:
                {
                    if (entityType.IsAssignableTo(typeof(IHasModificationTime))
                        && ((IHasModificationTime)entry.Entity).LastModificationTime == null)
                    {
                        entityType.GetProperty(name: "LastModificationTime")?.SetValue(entry.Entity, DateTime.UtcNow);
                    }

                    break;
                }

                case EntityState.Added:
                {
                    if (entityType.IsAssignableTo(typeof(IHasCreationTime)) && ((IHasCreationTime)entry.Entity).CreationTime == default)
                    {
                        entityType.GetProperty(name: "CreationTime")?.SetValue(entry.Entity, DateTime.UtcNow);
                    }

                    break;
                }
            }
        }
    }
}