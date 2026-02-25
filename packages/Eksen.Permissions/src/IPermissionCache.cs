using Eksen.Entities.Users;

namespace Eksen.Permissions;

public sealed class PermissionsDefinitionsCache
{
    public required List<PermissionDefinition> Permissions { get; init; }
}

public sealed class GrantedPermissionsCache
{
    public required List<string> Permissions { get; init; }
}

public interface IPermissionCache
{
    ValueTask InvalidateForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);

    ValueTask InvalidateForCurrentUserAsync(
        CancellationToken cancellationToken = default);

    ValueTask InvalidateForPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetPermissionsForCurrentUserAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PermissionDefinition>> GetPermissionDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);

    ValueTask InvalidateForUserIdsAsync(
        ICollection<EksenUserId> userIds,
        CancellationToken cancellationToken = default);
}