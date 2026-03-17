using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Eksen.Identity;
using Eksen.Identity.Users;
using Eksen.Repositories;
using Eksen.TestBase;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Auditing.Tests;

public class AuditLogManagerTests : EksenUnitTestBase
{
    private readonly Mock<IAuthContext> _authContext = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepository = new();
    private readonly Mock<IAuditLogActionRepository> _auditLogActionRepository = new();
    private readonly Mock<IAuditLogEntityChangeRepository> _auditLogEntityChangeRepository = new();
    private readonly Mock<IAuditLogPropertyChangeRepository> _auditLogPropertyChangeRepository = new();
    private readonly EksenAuditingOptions _options = new();

    private AuditLogManager CreateManager()
    {
        return new AuditLogManager(
            _authContext.Object,
            _auditLogRepository.Object,
            _auditLogActionRepository.Object,
            _auditLogEntityChangeRepository.Object,
            _auditLogPropertyChangeRepository.Object,
            Options.Create(_options));
    }

    [Fact]
    public void CurrentScope_Should_Be_Null_Before_BeginScope()
    {
        // Arrange
        var manager = CreateManager();

        // Act & Assert
        manager.CurrentScope.ShouldBeNull();
    }

    [Fact]
    public void BeginScope_Should_Create_New_Scope()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        var scope = manager.BeginScope();

        // Assert
        scope.ShouldNotBeNull();
        manager.CurrentScope.ShouldBe(scope);
        scope.AuditLog.ShouldNotBeNull();
    }

    [Fact]
    public void BeginScope_Should_Use_AuthContext_UserId()
    {
        // Arrange
        var userId = EksenUserId.NewId();
        var mockUser = new Mock<IAuthContextUser>();
        mockUser.Setup(u => u.UserId).Returns(userId);
        _authContext.Setup(a => a.User).Returns(mockUser.Object);

        var manager = CreateManager();

        // Act
        var scope = manager.BeginScope();

        // Assert
        scope.AuditLog.UserId.ShouldBe(userId);
    }

    [Fact]
    public void BeginScope_Should_Set_Null_UserId_When_Not_Authenticated()
    {
        // Arrange
        _authContext.Setup(a => a.User).Returns((IAuthContextUser?)null);

        var manager = CreateManager();

        // Act
        var scope = manager.BeginScope();

        // Assert
        scope.AuditLog.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_Should_Insert_AuditLog_When_Scope_Exists()
    {
        // Arrange
        _auditLogRepository
            .Setup(r => r.InsertAsync(
                It.IsAny<AuditLog>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = CreateManager();
        manager.BeginScope();

        // Act
        await manager.SaveAsync();

        // Assert
        _auditLogRepository.Verify(
            r => r.InsertAsync(It.IsAny<AuditLog>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_Should_Clear_CurrentScope_After_Save()
    {
        // Arrange
        _auditLogRepository
            .Setup(r => r.InsertAsync(
                It.IsAny<AuditLog>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = CreateManager();
        manager.BeginScope();

        // Act
        await manager.SaveAsync();

        // Assert
        manager.CurrentScope.ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_Should_Not_Insert_When_No_Scope_Exists()
    {
        // Arrange
        var manager = CreateManager();

        // Act
        await manager.SaveAsync();

        // Assert
        _auditLogRepository.Verify(
            r => r.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveAsync_Should_Not_Insert_When_Auditing_Is_Disabled()
    {
        // Arrange
        _options.IsEnabled = false;

        var manager = CreateManager();
        manager.BeginScope();

        // Act
        await manager.SaveAsync();

        // Assert
        _auditLogRepository.Verify(
            r => r.InsertAsync(It.IsAny<AuditLog>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAuditLogByIdAsync_Should_Delegate_To_Repository()
    {
        // Arrange
        var auditLogId = AuditLogId.NewId();
        var expectedAuditLog = new AuditLog(null, null, null, null, null);

        _auditLogRepository
            .Setup(r => r.FindAsync(
                auditLogId,
                It.IsAny<AuditLogFilterParameters?>(),
                It.IsAny<DefaultIncludeOptions<AuditLog>?>(),
                It.IsAny<DefaultQueryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAuditLog);

        var manager = CreateManager();

        // Act
        var result = await manager.GetAuditLogByIdAsync(auditLogId);

        // Assert
        result.ShouldBe(expectedAuditLog);

        _auditLogRepository.Verify(
            r => r.FindAsync(
                auditLogId,
                It.IsAny<AuditLogFilterParameters?>(),
                It.IsAny<DefaultIncludeOptions<AuditLog>?>(),
                It.IsAny<DefaultQueryOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAuditLogsAsync_Should_Delegate_To_Repository()
    {
        // Arrange
        var filterParameters = new AuditLogFilterParameters { CorrelationId = "corr-123" };
        var expectedLogs = new List<AuditLog> { new(null, null, null, null, null) };

        _auditLogRepository
            .Setup(r => r.GetListAsync(
                filterParameters,
                It.IsAny<DefaultIncludeOptions<AuditLog>?>(),
                It.IsAny<DefaultPaginationParameters?>(),
                It.IsAny<DefaultSortingParameters<AuditLog>?>(),
                It.IsAny<DefaultQueryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLogs);

        var manager = CreateManager();

        // Act
        var result = await manager.GetAuditLogsAsync(filterParameters);

        // Assert
        result.ShouldBe(expectedLogs);

        _auditLogRepository.Verify(
            r => r.GetListAsync(
                filterParameters,
                It.IsAny<DefaultIncludeOptions<AuditLog>?>(),
                It.IsAny<DefaultPaginationParameters?>(),
                It.IsAny<DefaultSortingParameters<AuditLog>?>(),
                It.IsAny<DefaultQueryOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEntityChangesAsync_Generic_Should_Delegate_To_Repository()
    {
        // Arrange
        var expectedChanges = new List<AuditLogEntityChange>
        {
            new(AuditLogId.NewId(), EntityChangeType.Created, typeof(TestEntityForAudit).FullName!, "entity-1")
        };

        _auditLogEntityChangeRepository
            .Setup(r => r.GetByEntityAsync(
                typeof(TestEntityForAudit).FullName!,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChanges);

        var manager = CreateManager();

        // Act
        var result = await manager.GetEntityChangesAsync<TestEntityForAudit>();

        // Assert
        result.ShouldBe(expectedChanges);

        _auditLogEntityChangeRepository.Verify(
            r => r.GetByEntityAsync(typeof(TestEntityForAudit).FullName!, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEntityChangesAsync_WithEntityId_Should_Delegate_To_Repository()
    {
        // Arrange
        var entityId = "entity-123";
        var expectedChanges = new List<AuditLogEntityChange>();

        _auditLogEntityChangeRepository
            .Setup(r => r.GetByEntityAsync(
                typeof(TestEntityForAudit).FullName!,
                entityId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChanges);

        var manager = CreateManager();

        // Act
        var result = await manager.GetEntityChangesAsync(typeof(TestEntityForAudit), entityId);

        // Assert
        result.ShouldBe(expectedChanges);

        _auditLogEntityChangeRepository.Verify(
            r => r.GetByEntityAsync(typeof(TestEntityForAudit).FullName!, entityId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActionsForAuditLogAsync_Should_Delegate_To_Repository()
    {
        // Arrange
        var auditLogId = AuditLogId.NewId();
        var expectedActions = new List<AuditLogAction>
        {
            new(auditLogId, "Service", "Method", null)
        };

        _auditLogActionRepository
            .Setup(r => r.GetByAuditLogIdAsync(
                auditLogId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedActions);

        var manager = CreateManager();

        // Act
        var result = await manager.GetActionsForAuditLogAsync(auditLogId);

        // Assert
        result.ShouldBe(expectedActions);

        _auditLogActionRepository.Verify(
            r => r.GetByAuditLogIdAsync(auditLogId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPropertyChangesForEntityChangeAsync_Should_Delegate_To_Repository()
    {
        // Arrange
        var entityChangeId = AuditLogEntityChangeId.NewId();
        var expectedChanges = new List<AuditLogPropertyChange>
        {
            new(entityChangeId, "Status", "System.String", "Old", "New")
        };

        _auditLogPropertyChangeRepository
            .Setup(r => r.GetByEntityChangeIdAsync(
                entityChangeId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChanges);

        var manager = CreateManager();

        // Act
        var result = await manager.GetPropertyChangesForEntityChangeAsync(entityChangeId);

        // Assert
        result.ShouldBe(expectedChanges);

        _auditLogPropertyChangeRepository.Verify(
            r => r.GetByEntityChangeIdAsync(entityChangeId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class TestEntityForAudit;
