using Eksen.Identity;
using Eksen.Permissions.Caching;
using Eksen.TestBase;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;

namespace Eksen.Permissions.Tests;

public sealed class DistributedPermissionCacheTests : EksenUnitTestBase
{
    [Fact]
    public async Task DefinitionsRoundTripPreservesIdsNamesAndFlagsAndIgnoresLegacyCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        await using var provider = services.BuildServiceProvider();
        var distributedCache = provider.GetRequiredService<IDistributedCache>();
        await distributedCache.SetStringAsync("permissions", "legacy incompatible payload");
        var permission = new PermissionDefinition(PermissionName.Create("Proof.Read"));
        permission.SetIsEnabled(false);
        typeof(PermissionDefinition).GetProperty(nameof(PermissionDefinition.IsDeleted))!.SetValue(permission, true);
        var store = new Mock<IPermissionStore>();
        store.Setup(x => x.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([permission]);
        var cache = new DistributedPermissionCache(new Mock<IAuthContext>().Object,
            new Lazy<IPermissionStore>(() => store.Object), distributedCache);

        await cache.GetPermissionDefinitionsAsync();
        var restored = (await cache.GetPermissionDefinitionsAsync()).Single();

        restored.Id.ShouldBe(permission.Id);
        restored.Name.ShouldBe(permission.Name);
        restored.IsDisabled.ShouldBeTrue();
        restored.IsDeleted.ShouldBeTrue();
        store.Verify(x => x.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
