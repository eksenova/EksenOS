using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Eksen.SmartEnums;
using Eksen.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Eksen.Permissions.AspNetCore;

public sealed class BindPermissionResultFilter(
    IPermissionChecker permissionChecker
) : IAsyncResultFilter
{
    private static readonly ConcurrentDictionary<Type, TypePlan> Plans = new();

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null, StatusCode: null or >= 200 and < 300 } obj)
        {
            await NullifyForbiddenAsync(obj.Value!, context.HttpContext.RequestAborted);
        }

        await next();
    }

    private async Task NullifyForbiddenAsync(object root, CancellationToken ct)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<object?>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var current = stack.Pop();
            if (current is null)
            {
                continue;
            }

            if (current is string)
            {
                continue;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (current is IEnumerable e and not IDictionary)
            {
                foreach (var item in e)
                {
                    if (item is not null) stack.Push(item);
                }

                continue;
            }

            var type = current.GetType();

            if (IsLeaf(type))
            {
                continue;
            }

            var plan = Plans.GetOrAdd(type, BuildPlan);

            if (plan.PermissionBoundProps.Length > 0)
            {
                var unique = plan.PermissionBoundProps
                    .Select(p => p.PermissionName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var checks = unique.ToDictionary(
                    p => p,
                    p => permissionChecker.HasPermissionAsync(p),
                    StringComparer.Ordinal);

                await Task.WhenAll(checks.Values);

                foreach (var p in plan.PermissionBoundProps)
                {
                    if (!await checks[p.PermissionName] && p.CanSetNull)
                    {
                        p.SetNull(current);
                    }
                }
            }

            foreach (var getter in plan.NestedGetters)
            {
                var nested = getter(current);
                if (nested is null)
                {
                    continue;
                }

                if (nested is string)
                {
                    continue;
                }

                if (nested is IEnumerable nestedEnum and not IDictionary)
                {
                    foreach (var item in nestedEnum)
                    {
                        if (item is not null) stack.Push(item);
                    }
                }
                else
                {
                    stack.Push(nested);
                }
            }
        }
    }

    private static TypePlan BuildPlan(Type t)
    {
        var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(p => p.GetIndexParameters().Length == 0)
                     .ToArray();

        var permissionBound = new List<PermissionProp>();
        var nestedGetters = new List<Func<object, object?>>();

        foreach (var p in props)
        {
            if (p is { CanRead: true, GetMethod: not null }
                && !IsLeaf(p.PropertyType)
                && p.PropertyType != typeof(string))
            {
                var getMethod = p.GetMethod;
                nestedGetters.Add(obj => getMethod.Invoke(obj, parameters: null));
            }

            var attr = p.GetCustomAttribute<BindPermissionAttribute>(inherit: true);
            if (attr is null)
            {
                continue;
            }

            var canSetNull = p is { CanWrite: true, SetMethod: not null } && CanAcceptNull(p);
            if (!canSetNull)
            {
                throw new InvalidOperationException($"Cannot set non-nullable property to null due to missing permission: {attr.PermissionName}");
            }

            var setMethod = p.SetMethod!;
            permissionBound.Add(new PermissionProp(
                attr.PermissionName,
                canSetNull,
                obj => setMethod.Invoke(obj, [null])
            ));
        }

        return new TypePlan(permissionBound.ToArray(), nestedGetters.ToArray());
    }

    private static bool CanAcceptNull(PropertyInfo p)
    {
        var pt = p.PropertyType;
        if (!pt.IsValueType)
        {
            return true;
        }   
        
        return Nullable.GetUnderlyingType(pt) is not null; 
    }

    private static bool IsLeaf(Type t)
    {
        if (t.IsPrimitive || t.IsEnum || t.IsEnumeration || t.IsConcreteValueObject)
        {
            return true;
        }

        if (t == typeof(string) 
            || t == typeof(decimal) 
            || t == typeof(TimeSpan)
            || t == typeof(DateTime) 
            || t == typeof(DateTimeOffset)
            || t == typeof(Guid) 
            || t == typeof(System.Ulid)
        )
        {
            return true;
        }

        return false;
    }

    private sealed record TypePlan(
        PermissionProp[] PermissionBoundProps,
        Func<object, object?>[] NestedGetters);

    private sealed record PermissionProp(
        string PermissionName,
        bool CanSetNull,
        Action<object> SetNull);
}
