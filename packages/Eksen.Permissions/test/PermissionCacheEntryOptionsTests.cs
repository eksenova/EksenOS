using Eksen.Permissions.Caching;
using Eksen.TestBase;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class PermissionCacheEntryOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Implicit_Conversion_To_DistributedCacheEntryOptions_Should_Work()
    {
        // Arrange
        var expiration = DateTimeOffset.Now.AddHours(1);
        var options = new PermissionCacheEntryOptions { AbsoluteExpiration = expiration };

        // Act
        DistributedCacheEntryOptions distributed = options;

        // Assert
        distributed.AbsoluteExpiration.ShouldBe(expiration);
    }

    [Fact]
    public void Implicit_Conversion_To_MemoryCacheEntryOptions_Should_Work()
    {
        // Arrange
        var expiration = DateTimeOffset.Now.AddHours(1);
        var options = new PermissionCacheEntryOptions { AbsoluteExpiration = expiration };

        // Act
        MemoryCacheEntryOptions memory = options;

        // Assert
        memory.AbsoluteExpiration.ShouldBe(expiration);
    }

    [Fact]
    public void Null_Expiration_Should_Be_Preserved()
    {
        // Arrange
        var options = new PermissionCacheEntryOptions { AbsoluteExpiration = null };

        // Act
        DistributedCacheEntryOptions distributed = options;
        MemoryCacheEntryOptions memory = options;

        // Assert
        distributed.AbsoluteExpiration.ShouldBeNull();
        memory.AbsoluteExpiration.ShouldBeNull();
    }
}
