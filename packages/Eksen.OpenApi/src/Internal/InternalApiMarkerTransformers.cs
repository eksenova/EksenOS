using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi.Internal;

/// <summary>
/// Marks operations of internal endpoints so document transformers can tell them apart.
/// </summary>
public sealed class InternalApiOperationMarkerTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!InternalApi.IsInternal(context.Description))
        {
            return Task.CompletedTask;
        }

        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        operation.Extensions[InternalApi.MarkerExtension] = new JsonNodeExtension(JsonValue.Create(true));

        return Task.CompletedTask;
    }
}

/// <summary>
/// Marks the component schemas of contract types that carry <see cref="InternalApiAttribute"/>.
/// </summary>
public sealed class InternalApiSchemaMarkerTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonPropertyInfo != null || !InternalApi.IsInternal(context.JsonTypeInfo.Type))
        {
            return Task.CompletedTask;
        }

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
        schema.Extensions[InternalApi.MarkerExtension] = new JsonNodeExtension(JsonValue.Create(true));

        return Task.CompletedTask;
    }
}
