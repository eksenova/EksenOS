using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Eksen.Identity;

public class EksenRoleManager<TRole, TTenant>(
    IRoleStore<TRole> store,
    IEnumerable<IRoleValidator<TRole>> roleValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    ILogger<EksenRoleManager<TRole, TTenant>> logger)
    : RoleManager<TRole>(store, roleValidators, keyNormalizer, errors, logger)
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant;