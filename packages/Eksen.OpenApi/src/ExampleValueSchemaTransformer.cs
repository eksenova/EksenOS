using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ExampleValueAttribute(object? value) : Attribute
{
    public object? Value { get; } = value;
}

public sealed class ExampleValueSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var attributes = context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(ExampleValueAttribute), inherit: true)
                         ?? [];

        var defaultValueAttribute = attributes.FirstOrDefault() as ExampleValueAttribute;
        if (defaultValueAttribute?.Value != null)
        {
            var exampleValue = defaultValueAttribute.Value;

            schema.Example = JsonSerializer.SerializeToNode(exampleValue, context.JsonTypeInfo);
        }

        return Task.CompletedTask;
    }
}