using Eksen.TestBase;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.DistributedLocks.PostgreSql.Tests;

[Collection(PostgreSqlCollection.Name)]
public class PostgreSqlDistributedLockProviderTests(PostgreSqlFixture fixture) : EksenUnitTestBase
{
    private IDistributedLockProvider CreateProvider(
        TimeSpan? defaultTimeout = null)
    {
        var pgOptions = Options.Create(new PostgreSqlDistributedLockOptions
        {
            ConnectionString = fixture.ConnectionString
        });

        var lockOptions = Options.Create(new EksenDistributedLockOptions
        {
            DefaultTimeout = defaultTimeout
        });

        return new PostgreSqlDistributedLockProvider(pgOptions, lockOptions);
    }

    [Fact]
    public async Task AcquireAsync_Should_Acquire_Lock_With_Explicit_Name()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await using var handle = await provider.AcquireAsync("test-lock-1");

        // Assert
        handle.ShouldNotBeNull();
        handle.IsAcquired.ShouldBeTrue();
        handle.Name.ShouldBe("test-lock-1");
    }

    [Fact]
    public async Task AcquireAsync_Should_Acquire_Lock_With_Generated_Name()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await using var handle = await provider.AcquireAsync();

        // Assert
        handle.ShouldNotBeNull();
        handle.IsAcquired.ShouldBeTrue();
        handle.Name.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AcquireAsync_Should_Throw_When_Timeout_Expires()
    {
        // Arrange
        var provider = CreateProvider();
        await using var firstHandle = await provider.AcquireAsync("timeout-lock");

        // Act & Assert
        await Should.ThrowAsync<DistributedLockException>(
            () => provider.AcquireAsync("timeout-lock", timeout: TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public async Task AcquireAsync_Should_Use_DefaultTimeout_From_Options()
    {
        // Arrange
        var provider = CreateProvider(defaultTimeout: TimeSpan.FromMilliseconds(200));
        await using var firstHandle = await provider.AcquireAsync("default-timeout-lock");

        // Act & Assert
        await Should.ThrowAsync<DistributedLockException>(
            () => provider.AcquireAsync("default-timeout-lock"));
    }

    [Fact]
    public async Task AcquireAsync_Should_Succeed_After_Previous_Lock_Released()
    {
        // Arrange
        var provider = CreateProvider();
        var handle1 = await provider.AcquireAsync("reuse-lock");
        await handle1.DisposeAsync();

        // Act
        await using var handle2 = await provider.AcquireAsync("reuse-lock");

        // Assert
        handle2.IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_Should_Acquire_Lock_Successfully()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await using var handle = await provider.TryAcquireAsync("try-lock-1");

        // Assert
        handle.ShouldNotBeNull();
        handle.IsAcquired.ShouldBeTrue();
        handle.Name.ShouldBe("try-lock-1");
    }

    [Fact]
    public async Task TryAcquireAsync_Should_Return_NotAcquired_When_Lock_Held()
    {
        // Arrange
        var provider = CreateProvider();
        await using var firstHandle = await provider.AcquireAsync("try-contended");

        // Act
        await using var secondHandle = await provider.TryAcquireAsync("try-contended");

        // Assert
        secondHandle.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_Should_Return_NotAcquired_When_Timeout_Expires()
    {
        // Arrange
        var provider = CreateProvider();
        await using var firstHandle = await provider.AcquireAsync("try-timeout-lock");

        // Act
        await using var secondHandle = await provider.TryAcquireAsync(
            "try-timeout-lock", timeout: TimeSpan.FromMilliseconds(200));

        // Assert
        secondHandle.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_Should_Use_DefaultTimeout_From_Options()
    {
        // Arrange
        var provider = CreateProvider(defaultTimeout: TimeSpan.FromMilliseconds(200));
        await using var firstHandle = await provider.AcquireAsync("try-default-timeout");

        // Act
        await using var secondHandle = await provider.TryAcquireAsync("try-default-timeout");

        // Assert
        secondHandle.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_Should_Generate_Name_When_Null()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await using var handle = await provider.TryAcquireAsync();

        // Assert
        handle.IsAcquired.ShouldBeTrue();
        handle.Name.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_IsAcquired_Should_Be_False_After_Dispose()
    {
        // Arrange
        var provider = CreateProvider();
        var handle = await provider.AcquireAsync("dispose-test");

        // Act
        await handle.DisposeAsync();

        // Assert
        handle.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_DisposeAsync_Should_Be_Idempotent()
    {
        // Arrange
        var provider = CreateProvider();
        var handle = await provider.AcquireAsync("idempotent-dispose");

        // Act & Assert
        await handle.DisposeAsync();
        await handle.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task AcquireAsync_Should_Allow_Different_Lock_Names_Concurrently()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await using var handle1 = await provider.AcquireAsync("concurrent-a");
        await using var handle2 = await provider.AcquireAsync("concurrent-b");

        // Assert
        handle1.IsAcquired.ShouldBeTrue();
        handle2.IsAcquired.ShouldBeTrue();
    }
}
