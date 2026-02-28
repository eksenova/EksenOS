using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Hashing;
using Microsoft.AspNetCore.Identity;

namespace Eksen.Identity;

public class EksenUserStore<TUser, TTenant>(
    IEksenUserRepository<TUser, TTenant> userRepository
) : IUserPasswordStore<TUser>,
    IUserEmailStore<TUser>,
    IUserRoleStore<TUser>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken)
    {
        return Task.FromException<bool>(new NotImplementedException());
    }

    public Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(user.EmailAddress?.Value);
    }

    public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromException<bool>(new NotImplementedException());
    }

    public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        return Task.FromException(new NotImplementedException());
    }

    public async Task<TUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        normalizedEmail = normalizedEmail
            .Replace(oldValue: "%", string.Empty)
            .Replace(oldValue: "?", string.Empty);

        var emailAddress = EmailAddress.Parse(normalizedEmail);

        cancellationToken.ThrowIfCancellationRequested();
        return await userRepository.FindByEmailAddressAsync(emailAddress, cancellationToken: cancellationToken);
    }

    public Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(user.EmailAddress?.Value.ToUpperInvariant());
    }

    public Task SetNormalizedEmailAsync(TUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        return Task.FromException<string?>(new NotSupportedException());
    }

    public async Task SetPasswordHashAsync(TUser user, string? passwordHashValue, CancellationToken cancellationToken)
    {
        var passwordHash = !string.IsNullOrWhiteSpace(passwordHashValue)
            ? PasswordHash.Create(passwordHashValue)
            : null;

        user.SetPasswordHash(passwordHash);

        await userRepository.UpdateAsync(user, autoSave: true, cancellationToken);
    }

    public Task<string?> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(user.PasswordHash?.Value);
    }

    public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(user.PasswordHash != null);
    }

    public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(user.Id.Value.ToString());
    }

    public virtual Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        return GetEmailAsync(user, cancellationToken);
    }

    public virtual Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken)
    {
        return SetEmailAsync(user, userName, cancellationToken);
    }

    public virtual Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return user.EmailAddress != null
            ? Task.FromResult<string?>(user.EmailAddress.Value.ToUpperInvariant())
            : Task.FromResult<string?>(result: null);
    }

    public virtual Task SetNormalizedUserNameAsync(TUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        return Task.FromException(new NotSupportedException());
    }

    public virtual Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromException<IdentityResult>(new NotImplementedException());
    }

    public virtual Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromException<IdentityResult>(new NotImplementedException());
    }

    public virtual Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromException<IdentityResult>(new NotImplementedException());
    }

    public virtual async Task<TUser?> FindByIdAsync(string userIdString, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (EksenUserId.TryParse(userIdString, provider: null, out var userId))
        {
            return await userRepository.FindAsync(
                userId,
                includeOptions: new EksenUserIncludeOptions<TUser, TTenant>
                {
                    IncludeTenant = true
                },
                cancellationToken: cancellationToken);
        }

        return null;
    }

    public virtual Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return FindByEmailAsync(normalizedUserName, cancellationToken);
    }

    public void Dispose() { }

    public virtual Task AddToRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        return Task.FromException(new NotImplementedException());
    }

    public virtual Task RemoveFromRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        return Task.FromException(new NotImplementedException());
    }

    public virtual Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromException<IList<string>>(new NotImplementedException());
    }

    public virtual Task<bool> IsInRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        return Task.FromException<bool>(new NotImplementedException());
    }

    public virtual Task<IList<TUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        return Task.FromException<IList<TUser>>(new NotImplementedException());
    }
}