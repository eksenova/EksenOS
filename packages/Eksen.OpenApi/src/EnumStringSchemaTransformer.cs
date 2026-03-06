using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Eksen.Core.Helpers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi;

public sealed class EnumStringSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var schemaClrType = TypeHelper.GetUnderlyingType(context.JsonTypeInfo.Type);
        if (!schemaClrType.IsEnum)
        {
            return Task.CompletedTask;
        }

        var openApiSchema = schema;
        openApiSchema.Type |= JsonSchemaType.String;
        openApiSchema.Type &= ~JsonSchemaType.Integer;

        openApiSchema.Enum = schemaClrType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => new { field, attr = field.GetCustomAttribute<EnumMemberAttribute>() })
            .Select(t => t.attr?.Value ?? t.field.Name)
            .Select(x => JsonValue.Create(x))
            .Cast<JsonNode>()
            .ToList();

        return Task.CompletedTask;
    }
}