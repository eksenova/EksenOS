using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Eksen.OpenApi.Internal;

/// <summary>
/// Endpoint metadata that keeps an operation out of public OpenAPI documents. Put
/// <see cref="InternalApiAttribute"/> on a controller, action or contract type, or add an instance of
/// this interface to endpoint metadata from a convention.
/// </summary>
public interface IInternalApiMetadata;

/// <summary>
/// Marks a controller, action or contract type as internal. Internal operations appear only in documents
/// registered with <see cref="OpenApiDocumentScope.Internal"/>. An internal contract type is dropped from
/// polymorphic unions in public documents and disappears from them once no public operation references it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class InternalApiAttribute : Attribute, IInternalApiMetadata;

/// <summary>
/// Lookups shared by the internal-API transformers.
/// </summary>
public static class InternalApi
{
    /// <summary>
    /// The vendor extension that carries the internal marker between transformers. It is removed before the
    /// document is served.
    /// </summary>
    public const string MarkerExtension = "x-eksen-internal";

    /// <summary>
    /// Returns <see langword="true"/> when the described endpoint carries <see cref="IInternalApiMetadata"/>,
    /// either as endpoint metadata or as an attribute on the action method or controller type.
    /// </summary>
    public static bool IsInternal(ApiDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);

        if (description.ActionDescriptor.EndpointMetadata.OfType<IInternalApiMetadata>().Any())
        {
            return true;
        }

        if (description.ActionDescriptor is not ControllerActionDescriptor action)
        {
            return false;
        }

        return action.MethodInfo.GetCustomAttributes(inherit: true).OfType<IInternalApiMetadata>().Any()
               || action.ControllerTypeInfo.GetCustomAttributes(inherit: true).OfType<IInternalApiMetadata>().Any();
    }

    /// <summary>
    /// Returns <see langword="true"/> when the contract type is marked internal.
    /// </summary>
    public static bool IsInternal(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.GetCustomAttributes(inherit: false).OfType<IInternalApiMetadata>().Any();
    }
}
