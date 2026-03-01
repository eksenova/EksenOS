using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.Ulid.OpenApi;

internal sealed class UlidSchemaTransformer : IOpenApiSchemaTransformer
{
    public const string UlidFormat = "ulid";
    public const string Example = "01KD0ZX9P61N7AHAV35R79XGQM";

    public Task TransformAsync(
        OpenApiSchema schema, 
        OpenApiSchemaTransformerContext context, 
        CancellationToken cancellationToken)
    {
        var schemaClrType = context.JsonTypeInfo.Type;
        var hasUlidAttribute = context.JsonPropertyInfo?.AttributeProvider?.IsDefined(typeof(UlidAttribute), inherit: true)
                               ?? false;
        
        if (schemaClrType != typeof(System.Ulid) && !hasUlidAttribute)
        {
            return Task.CompletedTask;
        }

        Transform(schema);
        return Task.CompletedTask;
    }

    public static void Transform(OpenApiSchema schema)
    {
        schema.Example ??= Example;
        schema.MinLength = UlidConsts.Length;
        schema.MaxLength = UlidConsts.Length;
        schema.Type = JsonSchemaType.String;
        schema.Format = UlidFormat;
    }
}