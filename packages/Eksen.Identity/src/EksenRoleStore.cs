using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Eksen.Identity;

public class EksenRoleStore<TRole, TTenant>(
    ILogger<EksenRoleStore<TRole, TTenant>> logger,
    IEksenRoleRepository<TRole, TTenant> roleRepository,
    IdentityErrorDescriber? errorDescriber = null)
    : IRoleStore<TRole>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    private readonly IdentityErrorDescriber _errorDescriber = errorDescriber ?? new IdentityErrorDescriber();

    public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await roleRepository.InsertAsync(role, autoSave: true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, message: "Failed to create role");
            return IdentityResult.Failed(_errorDescriber.ConcurrencyFailure());
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await roleRepository.UpdateAsync(role, autoSave: true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, message: "Failed to update role");
            return IdentityResult.Failed(_errorDescriber.ConcurrencyFailure());
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await roleRepository.RemoveAsync(role, autoSave: true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, message: "Failed to delete role");
            return IdentityResult.Failed(_errorDescriber.ConcurrencyFailure());
        }


        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(role.Id.Value.ToString());
    }

    public Task<string?> GetRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(role.Name.Value);
    }

    public async Task SetRoleNameAsync(TRole role, string? roleNameValue, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var roleName = RoleName.Parse(roleNameValue!);

        role.SetName(roleName);
        await roleRepository.UpdateAsync(role, autoSave: true, cancellationToken);
    }

    public Task<string?> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        return GetRoleNameAsync(role, cancellationToken);
    }

    public Task SetNormalizedRoleNameAsync(TRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // we don't have normalized role names
        return Task.CompletedTask;
    }

    public async Task<TRole?> FindByIdAsync(string roleIdValue, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (EksenRoleId.TryParse(roleIdValue, provider: null, out var roleId))
        {
            return (TRole?)await roleRepository.FindAsync(
                roleId,
                cancellationToken: cancellationToken);
        }

        throw new ArgumentException($"Invalid role id: {roleIdValue}. Role ID must be valid ULID.", nameof(roleIdValue));
    }

    public Task<TRole?> FindByNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        return Task.FromException<TRole?>(new NotSupportedException());
    }

    public void Dispose() { }
}