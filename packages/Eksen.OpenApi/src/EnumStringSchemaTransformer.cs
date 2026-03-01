using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EksenDefter.Infrastructure.OpenApi;

internal sealed class EnumStringSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var schemaClrType = context.JsonTypeInfo.Type;
        schemaClrType = Nullable.GetUnderlyingType(schemaClrType) ?? schemaClrType;

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