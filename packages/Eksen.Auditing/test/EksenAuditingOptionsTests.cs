using System.Reflection;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests;

public class EksenAuditingOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Default_IsEnabled_Should_Be_True()
    {
        // Arrange & Act
        var options = new EksenAuditingOptions();

        // Assert
        options.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Default_LogHttpRequestPayload_Should_Be_False()
    {
        // Arrange & Act
        var options = new EksenAuditingOptions();

        // Assert
        options.LogHttpRequestPayload.ShouldBeFalse();
    }

    [Fact]
    public void Default_LogMethodReturnValues_Should_Be_False()
    {
        // Arrange & Act
        var options = new EksenAuditingOptions();

        // Assert
        options.LogMethodReturnValues.ShouldBeFalse();
    }

    [Fact]
    public void Default_AuditedTypes_Should_Be_Empty()
    {
        // Arrange & Act
        var options = new EksenAuditingOptions();

        // Assert
        options.AuditedTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Add_Generic_Should_Add_Type_To_AuditedTypes()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.Add<AuditableService>();

        // Assert
        options.AuditedTypes.Count.ShouldBe(1);
        options.AuditedTypes.ShouldContain(typeof(AuditableService));
    }

    [Fact]
    public void Add_Type_Should_Add_Type_To_AuditedTypes()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.Add(typeof(AuditableService));

        // Assert
        options.AuditedTypes.Count.ShouldBe(1);
        options.AuditedTypes.ShouldContain(typeof(AuditableService));
    }

    [Fact]
    public void Add_Should_Not_Duplicate_Same_Type()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.Add<AuditableService>();
        options.Add<AuditableService>();

        // Assert
        options.AuditedTypes.Count.ShouldBe(1);
    }

    [Fact]
    public void AddAssembly_Should_Add_Non_Abstract_Classes()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.AddAssembly(typeof(AuditableService).Assembly);

        // Assert
        options.AuditedTypes.ShouldContain(typeof(AuditableService));
    }

    [Fact]
    public void AddAssembly_Should_Exclude_Abstract_Classes()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.AddAssembly(typeof(AbstractService).Assembly);

        // Assert
        options.AuditedTypes.ShouldNotContain(typeof(AbstractService));
    }

    [Fact]
    public void AddAssembly_Should_Exclude_Types_With_ExcludeAttribute()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.AddAssembly(typeof(ExcludedService).Assembly);

        // Assert
        options.AuditedTypes.ShouldNotContain(typeof(ExcludedService));
    }

    [Fact]
    public void AddAssembly_Should_Exclude_Interfaces()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.AddAssembly(typeof(IAuditableInterface).Assembly);

        // Assert
        options.AuditedTypes.ShouldNotContain(typeof(IAuditableInterface));
    }

    [Fact]
    public void AddAssembly_Should_Exclude_Structs()
    {
        // Arrange
        var options = new EksenAuditingOptions();

        // Act
        options.AddAssembly(typeof(AuditableStruct).Assembly);

        // Assert
        options.AuditedTypes.ShouldNotContain(typeof(AuditableStruct));
    }
}

// Test types used by EksenAuditingOptionsTests
public class AuditableService;

public abstract class AbstractService;

[ExcludeFromAuditLogs]
public class ExcludedService;

public interface IAuditableInterface;

public struct AuditableStruct;
