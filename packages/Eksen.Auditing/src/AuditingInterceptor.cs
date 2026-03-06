using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Castle.DynamicProxy;
using Eksen.Auditing.Entities;

namespace Eksen.Auditing;

public sealed class AuditingInterceptor(IAuditLogManager auditLogManager) : IAsyncInterceptor
{
    public void InterceptSynchronous(IInvocation invocation)
    {
        var scope = auditLogManager.CurrentScope;
        if (scope == null)
        {
            invocation.Proceed();
            return;
        }

        if (ShouldSkip(invocation.Method))
        {
            invocation.Proceed();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var parameters = SerializeParameters(invocation);

        var action = new AuditLogAction(
            scope.AuditLog.Id,
            invocation.TargetType?.FullName ?? invocation.Method.DeclaringType?.FullName ?? "Unknown",
            invocation.Method.Name,
            parameters);

        try
        {
            invocation.Proceed();

            stopwatch.Stop();
            action.SetDuration(stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            action.SetDuration(stopwatch.Elapsed);
            action.SetException(ex.Message);
            throw;
        }
        finally
        {
            scope.AddAction(action);
        }
    }

    public void InterceptAsynchronous(IInvocation invocation)
    {
        invocation.ReturnValue = InternalInterceptAsynchronous(invocation);
    }

    public void InterceptAsynchronous<TResult>(IInvocation invocation)
    {
        invocation.ReturnValue = InternalInterceptAsynchronous<TResult>(invocation);
    }

    private async Task InternalInterceptAsynchronous(IInvocation invocation)
    {
        var scope = auditLogManager.CurrentScope;
        if (scope == null)
        {
            invocation.Proceed();
            await (Task)invocation.ReturnValue;
            return;
        }

        if (ShouldSkip(invocation.Method))
        {
            invocation.Proceed();
            await (Task)invocation.ReturnValue;
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var parameters = SerializeParameters(invocation);

        var action = new AuditLogAction(
            scope.AuditLog.Id,
            invocation.TargetType?.FullName ?? invocation.Method.DeclaringType?.FullName ?? "Unknown",
            invocation.Method.Name,
            parameters);

        try
        {
            invocation.Proceed();
            await (Task)invocation.ReturnValue;

            stopwatch.Stop();
            action.SetDuration(stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            action.SetDuration(stopwatch.Elapsed);
            action.SetException(ex.Message);
            throw;
        }
        finally
        {
            scope.AddAction(action);
        }
    }

    private async Task<TResult> InternalInterceptAsynchronous<TResult>(IInvocation invocation)
    {
        var scope = auditLogManager.CurrentScope;
        if (scope == null)
        {
            invocation.Proceed();
            return await (Task<TResult>)invocation.ReturnValue;
        }

        if (ShouldSkip(invocation.Method))
        {
            invocation.Proceed();
            return await (Task<TResult>)invocation.ReturnValue;
        }

        var stopwatch = Stopwatch.StartNew();
        var parameters = SerializeParameters(invocation);

        var action = new AuditLogAction(
            scope.AuditLog.Id,
            invocation.TargetType?.FullName ?? invocation.Method.DeclaringType?.FullName ?? "Unknown",
            invocation.Method.Name,
            parameters);

        TResult result;

        try
        {
            invocation.Proceed();
            result = await (Task<TResult>)invocation.ReturnValue;

            stopwatch.Stop();
            action.SetDuration(stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            action.SetDuration(stopwatch.Elapsed);
            action.SetException(ex.Message);
            throw;
        }
        finally
        {
            scope.AddAction(action);
        }

        return result;
    }

    private static bool ShouldSkip(MethodInfo method)
    {
        return method.GetCustomAttribute<ExcludeFromAuditLogsAttribute>() != null
               || method.DeclaringType?.GetCustomAttribute<ExcludeFromAuditLogsAttribute>() != null;
    }

    private static string? SerializeParameters(IInvocation invocation)
    {
        var methodParams = invocation.Method.GetParameters();
        if (methodParams.Length == 0)
            return null;

        var paramDict = new Dictionary<string, object?>();

        for (var i = 0; i < methodParams.Length; i++)
        {
            var paramInfo = methodParams[i];

            if (paramInfo.GetCustomAttribute<ExcludeFromAuditLogsAttribute>() != null)
                continue;

            if (paramInfo.ParameterType == typeof(CancellationToken))
                continue;

            try
            {
                paramDict[paramInfo.Name ?? $"arg{i}"] = invocation.Arguments[i];
            }
            catch
            {
                paramDict[paramInfo.Name ?? $"arg{i}"] = "<not serializable>";
            }
        }

        if (paramDict.Count == 0)
            return null;

        try
        {
            return JsonSerializer.Serialize(paramDict, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            });
        }
        catch
        {
            return "<serialization failed>";
        }
    }
}