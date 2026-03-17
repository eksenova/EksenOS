using System.Reflection;
using Castle.DynamicProxy;
using Eksen.Auditing.Entities;
using Eksen.TestBase;
using Moq;
using Shouldly;
using CastleInvocation = Castle.DynamicProxy.IInvocation;

namespace Eksen.Auditing.Tests;

public class AuditingInterceptorTests : EksenUnitTestBase
{
    private readonly Mock<IAuditLogManager> _auditLogManager = new();

    private AuditingInterceptor CreateInterceptor()
    {
        return new AuditingInterceptor(_auditLogManager.Object);
    }

    #region InterceptSynchronous

    [Fact]
    public void InterceptSynchronous_Should_Proceed_Without_Action_When_No_Scope()
    {
        // Arrange
        _auditLogManager.Setup(m => m.CurrentScope).Returns((IAuditLogScope?)null);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocation(nameof(ISampleService.DoWork));

        // Act
        interceptor.InterceptSynchronous(invocation.Object);

        // Assert
        invocation.Verify(i => i.Proceed(), Times.Once);
    }

    [Fact]
    public void InterceptSynchronous_Should_Add_Action_To_Scope_On_Success()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocation(nameof(ISampleService.DoWork));

        // Act
        interceptor.InterceptSynchronous(invocation.Object);

        // Assert
        invocation.Verify(i => i.Proceed(), Times.Once);
        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.First().MethodName.ShouldBe(nameof(ISampleService.DoWork));
        auditLog.Actions.First().ExceptionMessage.ShouldBeNull();
        auditLog.Actions.First().Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void InterceptSynchronous_Should_Record_Exception_And_Rethrow()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocation(nameof(ISampleService.DoWork));
        invocation.Setup(i => i.Proceed()).Throws(new InvalidOperationException("Test error"));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => interceptor.InterceptSynchronous(invocation.Object));

        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.First().ExceptionMessage.ShouldBe("Test error");
    }

    [Fact]
    public void InterceptSynchronous_Should_Skip_When_DeclaringType_Has_ExcludeAttribute()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocation(
            nameof(ExcludedSampleService.DoWork),
            typeof(ExcludedSampleService));

        // Act
        interceptor.InterceptSynchronous(invocation.Object);

        // Assert
        invocation.Verify(i => i.Proceed(), Times.Once);
        auditLog.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void InterceptSynchronous_Should_Serialize_Parameters()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocationWithParameters(
            typeof(ISampleService).GetMethod(nameof(ISampleService.DoWorkWithParams))!,
            ["testValue", 42]);

        // Act
        interceptor.InterceptSynchronous(invocation.Object);

        // Assert
        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.First().Parameters.ShouldNotBeNull();
        auditLog.Actions.First().Parameters!.ShouldContain("testValue");
        auditLog.Actions.First().Parameters!.ShouldContain("42");
    }

    [Fact]
    public void InterceptSynchronous_Should_Skip_CancellationToken_Parameters()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocationWithParameters(
            typeof(ISampleService).GetMethod(nameof(ISampleService.DoWorkWithCancellation))!,
            ["value", CancellationToken.None]);

        // Act
        interceptor.InterceptSynchronous(invocation.Object);

        // Assert
        auditLog.Actions.Count.ShouldBe(1);
        var parameters = auditLog.Actions.First().Parameters;
        parameters.ShouldNotBeNull();
        parameters.ShouldContain("value");
        parameters.ShouldNotContain("CancellationToken");
    }

    [Fact]
    public void InterceptSynchronous_Should_Set_Null_Parameters_When_No_Method_Parameters()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocation(nameof(ISampleService.DoWork));

        // Act
        interceptor.InterceptSynchronous(invocation.Object);

        // Assert
        auditLog.Actions.First().Parameters.ShouldBeNull();
    }

    [Fact]
    public void InterceptSynchronous_Should_Record_ServiceType_From_TargetType()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateSynchronousInvocation(nameof(ISampleService.DoWork));

        // Act
        interceptor.InterceptSynchronous(invocation.Object);

        // Assert
        auditLog.Actions.First().ServiceType.ShouldBe(typeof(ISampleService).FullName);
    }

    #endregion

    #region InterceptAsynchronous (Task)

    [Fact]
    public async Task InterceptAsynchronous_Should_Proceed_Without_Action_When_No_Scope()
    {
        // Arrange
        _auditLogManager.Setup(m => m.CurrentScope).Returns((IAuditLogScope?)null);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncInvocation(nameof(ISampleService.DoWorkAsync));

        // Act
        interceptor.InterceptAsynchronous(invocation.Object);
        await (Task)invocation.Object.ReturnValue;

        // Assert
        invocation.Verify(i => i.Proceed(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsynchronous_Should_Add_Action_To_Scope_On_Success()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncInvocation(nameof(ISampleService.DoWorkAsync));

        // Act
        interceptor.InterceptAsynchronous(invocation.Object);
        await (Task)invocation.Object.ReturnValue;

        // Assert
        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.First().MethodName.ShouldBe(nameof(ISampleService.DoWorkAsync));
        auditLog.Actions.First().ExceptionMessage.ShouldBeNull();
    }

    [Fact]
    public async Task InterceptAsynchronous_Should_Record_Exception_And_Rethrow()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncInvocation(
            nameof(ISampleService.DoWorkAsync),
            Task.FromException(new InvalidOperationException("Async error")));

        // Act & Assert
        interceptor.InterceptAsynchronous(invocation.Object);
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await (Task)invocation.Object.ReturnValue);

        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.First().ExceptionMessage.ShouldBe("Async error");
    }

    [Fact]
    public async Task InterceptAsynchronous_Should_Skip_When_DeclaringType_Has_ExcludeAttribute()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncInvocation(
            nameof(ExcludedSampleService.DoWorkAsync),
            declaringType: typeof(ExcludedSampleService));

        // Act
        interceptor.InterceptAsynchronous(invocation.Object);
        await (Task)invocation.Object.ReturnValue;

        // Assert
        auditLog.Actions.ShouldBeEmpty();
    }

    #endregion

    #region InterceptAsynchronous<TResult> (Task<T>)

    [Fact]
    public async Task InterceptAsynchronousGeneric_Should_Proceed_Without_Action_When_No_Scope()
    {
        // Arrange
        _auditLogManager.Setup(m => m.CurrentScope).Returns((IAuditLogScope?)null);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncGenericInvocation(nameof(ISampleService.GetValueAsync), 42);

        // Act
        interceptor.InterceptAsynchronous<int>(invocation.Object);
        var result = await (Task<int>)invocation.Object.ReturnValue;

        // Assert
        result.ShouldBe(42);
        invocation.Verify(i => i.Proceed(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsynchronousGeneric_Should_Add_Action_To_Scope_On_Success()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncGenericInvocation(nameof(ISampleService.GetValueAsync), 42);

        // Act
        interceptor.InterceptAsynchronous<int>(invocation.Object);
        var result = await (Task<int>)invocation.Object.ReturnValue;

        // Assert
        result.ShouldBe(42);
        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.First().MethodName.ShouldBe(nameof(ISampleService.GetValueAsync));
        auditLog.Actions.First().ExceptionMessage.ShouldBeNull();
    }

    [Fact]
    public async Task InterceptAsynchronousGeneric_Should_Record_Exception_And_Rethrow()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncGenericInvocation<int>(
            nameof(ISampleService.GetValueAsync),
            exception: new InvalidOperationException("Async generic error"));

        // Act & Assert
        interceptor.InterceptAsynchronous<int>(invocation.Object);
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await (Task<int>)invocation.Object.ReturnValue);

        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.First().ExceptionMessage.ShouldBe("Async generic error");
    }

    [Fact]
    public async Task InterceptAsynchronousGeneric_Should_Skip_When_DeclaringType_Has_ExcludeAttribute()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        _auditLogManager.Setup(m => m.CurrentScope).Returns(scope);

        var interceptor = CreateInterceptor();
        var invocation = CreateAsyncGenericInvocation(
            nameof(ExcludedSampleService.GetValueAsync),
            "result",
            declaringType: typeof(ExcludedSampleService));

        // Act
        interceptor.InterceptAsynchronous<string>(invocation.Object);
        await (Task<string>)invocation.Object.ReturnValue;

        // Assert
        auditLog.Actions.ShouldBeEmpty();
    }

    #endregion

    #region Helpers

    private static Mock<CastleInvocation> CreateSynchronousInvocation(
        string methodName,
        Type? declaringType = null)
    {
        declaringType ??= typeof(ISampleService);
        var method = declaringType.GetMethod(methodName)!;
        var invocation = new Mock<CastleInvocation>();
        invocation.Setup(i => i.Method).Returns(method);
        invocation.Setup(i => i.TargetType).Returns(declaringType);
        invocation.Setup(i => i.Arguments).Returns([]);
        return invocation;
    }

    private static Mock<CastleInvocation> CreateSynchronousInvocationWithParameters(
        MethodInfo method,
        object[] arguments)
    {
        var invocation = new Mock<CastleInvocation>();
        invocation.Setup(i => i.Method).Returns(method);
        invocation.Setup(i => i.TargetType).Returns(typeof(ISampleService));
        invocation.Setup(i => i.Arguments).Returns(arguments);
        return invocation;
    }

    private static Mock<CastleInvocation> CreateAsyncInvocation(
        string methodName,
        Task? returnTask = null,
        Type? declaringType = null)
    {
        declaringType ??= typeof(ISampleService);
        var method = declaringType.GetMethod(methodName)!;
        var invocation = new Mock<CastleInvocation>();
        invocation.Setup(i => i.Method).Returns(method);
        invocation.Setup(i => i.TargetType).Returns(declaringType);
        invocation.Setup(i => i.Arguments).Returns([]);
        invocation.SetupProperty(i => i.ReturnValue, returnTask ?? Task.CompletedTask);
        return invocation;
    }

    private static Mock<CastleInvocation> CreateAsyncGenericInvocation<TResult>(
        string methodName,
        TResult? result = default,
        Exception? exception = null,
        Type? declaringType = null)
    {
        declaringType ??= typeof(ISampleService);
        var method = declaringType.GetMethod(methodName)!;
        var invocation = new Mock<CastleInvocation>();
        invocation.Setup(i => i.Method).Returns(method);
        invocation.Setup(i => i.TargetType).Returns(declaringType);
        invocation.Setup(i => i.Arguments).Returns([]);

        Task<TResult> returnTask = exception != null
            ? Task.FromException<TResult>(exception)
            : Task.FromResult(result!);

        invocation.SetupProperty(i => i.ReturnValue, returnTask);
        return invocation;
    }

    #endregion
}

public interface ISampleService
{
    void DoWork();

    void DoWorkWithParams(string name, int count);

    void DoWorkWithCancellation(string name, CancellationToken cancellationToken);

    Task DoWorkAsync();

    Task<int> GetValueAsync();
}

[ExcludeFromAuditLogs]
public class ExcludedSampleService
{
    public virtual void DoWork() { }

    public virtual Task DoWorkAsync() => Task.CompletedTask;

    public virtual Task<string> GetValueAsync() => Task.FromResult("excluded");
}
