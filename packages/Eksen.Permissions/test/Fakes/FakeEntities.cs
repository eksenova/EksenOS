using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Hashing;

namespace Eksen.Permissions.Tests.Fakes;

public class FakeTenant : IEksenTenant
{
    public EksenTenantId Id { get; set; } = new(System.Ulid.NewUlid());
    public TenantName Name { get; set; } = TenantName.Create("Test Tenant");
    public bool IsActive { get; set; } = true;
}

public class FakeRole : IEksenRole<FakeTenant>
{
    public EksenRoleId Id { get; set; } = new(System.Ulid.NewUlid());
    public RoleName Name { get; set; } = RoleName.Create("TestRole");
    public FakeTenant? Tenant { get; set; }

    public void SetName(RoleName roleName) => Name = roleName;
}

public class FakeUser : IEksenUser<FakeTenant>
{
    public EksenUserId Id { get; set; } = new(System.Ulid.NewUlid());
    public EmailAddress? EmailAddress { get; set; }
    public PasswordHash? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPasswordChangeRequired { get; set; }
    public FakeTenant? Tenant { get; set; }

    public void SetPasswordHash(PasswordHash? passwordHash) => PasswordHash = passwordHash;
    public void SetActive(bool isActive) => IsActive = isActive;
}
