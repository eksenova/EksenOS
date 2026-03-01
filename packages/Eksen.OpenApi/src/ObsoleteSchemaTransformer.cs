using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EksenDefter.Infrastructure.OpenApi;

internal sealed class ObsoleteSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemaClrType = context.JsonTypeInfo.Type;
        var obsoleteAttribute = schemaClrType.GetCustomAttribute<ObsoleteAttribute>(inherit: true);

        if (obsoleteAttribute != null)
        {
            schema.Deprecated = true;
            if (!string.IsNullOrWhiteSpace(schema.Description))
            {
                schema.Description += Environment.NewLine;
            }
            else
            {
                schema.Description = string.Empty;
            }

            schema.Description += $"**Deprecated**: {obsoleteAttribute.Message}";
        }

        return Task.CompletedTask;
    }
}