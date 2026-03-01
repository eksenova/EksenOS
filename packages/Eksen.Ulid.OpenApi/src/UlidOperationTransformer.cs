using Eksen.Ulid.AspNetCore;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.Ulid.OpenApi;

internal sealed class UlidOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters == null)
        {
            return Task.CompletedTask;
        }

        foreach (var baseParameter in operation.Parameters)
        {
            var parameter = (OpenApiParameter)baseParameter;
            var actionParameter = context.Description.ParameterDescriptions
                .FirstOrDefault(x => x.Name == parameter.Name);

            if (actionParameter == null || parameter.Schema == null)
            {
                continue;
            }

            var isUlidType = actionParameter.ParameterDescriptor.ParameterType == typeof(System.Ulid) 
                             || (actionParameter.RouteInfo?.Constraints?
                                 .Any(typeof(UlidRouteConstraint).IsInstanceOfType) ?? false);

            if (!isUlidType)
            {
                continue;
            }

            var schema = (OpenApiSchema)parameter.Schema;
            if (schema.Format != UlidSchemaTransformer.UlidFormat)
            {
                UlidSchemaTransformer.Transform(schema);
            }

            parameter.AllowEmptyValue = false;
            parameter.Example ??= schema.Example;
        }

        return Task.CompletedTask;
    }
}