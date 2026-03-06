using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.SmartEnums.OpenApi;

public sealed class SmartEnumOperationTransformer : IOpenApiOperationTransformer
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

            if (actionParameter?.ModelMetadata == null || parameter.Schema == null)
            {
                continue;
            }

            var parameterType = actionParameter.ModelMetadata.UnderlyingOrModelType;

            SmartEnumSchemaTransformer.ProcessSchema(
                (OpenApiSchema)parameter.Schema,
                parameterType,
                out _,
                out var isCollection,
                out _);

            if (isCollection)
            {
                parameter.Style = ParameterStyle.Form;
                parameter.Explode = true;
            }
        }

        return Task.CompletedTask;
    }
}