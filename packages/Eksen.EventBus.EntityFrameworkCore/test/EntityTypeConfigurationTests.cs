using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.EventBus.EntityFrameworkCore.Tests;

public class EntityTypeConfigurationTests : EventBusEfCoreTestBase
{
    [Fact]
    public void OutboxMessages_Table_Should_Be_Named_Correctly()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(OutboxMessageEntity))!;
        entityType.GetTableName().ShouldBe("EventBusOutboxMessages");
    }

    [Fact]
    public void InboxMessages_Table_Should_Be_Named_Correctly()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(InboxMessageEntity))!;
        entityType.GetTableName().ShouldBe("EventBusInboxMessages");
    }

    [Fact]
    public void DeadLetterMessages_Table_Should_Be_Named_Correctly()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(DeadLetterMessageEntity))!;
        entityType.GetTableName().ShouldBe("EventBusDeadLetterMessages");
    }

    [Fact]
    public void Outbox_EventType_Should_Have_MaxLength_512()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(OutboxMessageEntity))!;
        var property = entityType.FindProperty(nameof(OutboxMessageEntity.EventType))!;
        property.GetMaxLength().ShouldBe(512);
    }

    [Fact]
    public void Inbox_EventId_HandlerType_Should_Have_Unique_Index()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(InboxMessageEntity))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(InboxMessageEntity.EventId)) &&
                i.Properties.Any(p => p.Name == nameof(InboxMessageEntity.HandlerType)));

        index.ShouldNotBeNull();
        index.IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Outbox_Should_Have_Status_Index()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(OutboxMessageEntity))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Count == 1 &&
                i.Properties[0].Name == nameof(OutboxMessageEntity.Status));

        index.ShouldNotBeNull();
    }

    [Fact]
    public void Outbox_Should_Have_Composite_Status_CreationTime_Index()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(OutboxMessageEntity))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Count == 2 &&
                i.Properties[0].Name == nameof(OutboxMessageEntity.Status) &&
                i.Properties[1].Name == nameof(OutboxMessageEntity.CreationTime));

        index.ShouldNotBeNull();
    }

    [Fact]
    public void Inbox_Should_Have_Status_Index()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(InboxMessageEntity))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Count == 1 &&
                i.Properties[0].Name == nameof(InboxMessageEntity.Status));

        index.ShouldNotBeNull();
    }

    [Fact]
    public void DeadLetter_Should_Have_OriginalMessageId_Index()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(DeadLetterMessageEntity))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Count == 1 &&
                i.Properties[0].Name == nameof(DeadLetterMessageEntity.OriginalMessageId));

        index.ShouldNotBeNull();
    }

    [Fact]
    public void DeadLetter_Should_Have_IsRequeued_Index()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(DeadLetterMessageEntity))!;
        var index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Count == 1 &&
                i.Properties[0].Name == nameof(DeadLetterMessageEntity.IsRequeued));

        index.ShouldNotBeNull();
    }

    [Fact]
    public void Outbox_CorrelationId_Should_Have_MaxLength_64()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(OutboxMessageEntity))!;
        var property = entityType.FindProperty(nameof(OutboxMessageEntity.CorrelationId))!;
        property.GetMaxLength().ShouldBe(64);
    }

    [Fact]
    public void Outbox_SourceApp_Should_Have_MaxLength_256()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(OutboxMessageEntity))!;
        var property = entityType.FindProperty(nameof(OutboxMessageEntity.SourceApp))!;
        property.GetMaxLength().ShouldBe(256);
    }

    [Fact]
    public void Outbox_LastError_Should_Have_MaxLength_2048()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(OutboxMessageEntity))!;
        var property = entityType.FindProperty(nameof(OutboxMessageEntity.LastError))!;
        property.GetMaxLength().ShouldBe(2048);
    }

    [Fact]
    public void DeadLetter_LastError_Should_Be_Required()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(DeadLetterMessageEntity))!;
        var property = entityType.FindProperty(nameof(DeadLetterMessageEntity.LastError))!;
        property.IsNullable.ShouldBeFalse();
    }
}
