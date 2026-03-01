using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EksenDefter.Infrastructure.OpenApi;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ExampleValueAttribute(object? value) : Attribute
{
    public object? Value { get; } = value;
}

internal sealed class ExampleValueSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var attributes = context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(ExampleValueAttribute), inherit: true)
                         ?? [];

        var defaultValueAttribute = attributes.FirstOrDefault() as ExampleValueAttribute;
        if (defaultValueAttribute?.Value != null)
        {
            schema.Example = defaultValueAttribute.Value.ToString();
        }

        return Task.CompletedTask;
    }
}