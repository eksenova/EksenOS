using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi;

/// <summary>
/// Removes component schemas that no operation, parameter, header or other reachable schema references, so a
/// document filtered down to a subset of endpoints does not list models from the endpoints it left out.
/// </summary>
public sealed class OrphanSchemaDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemas = document.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
        {
            return Task.CompletedTask;
        }

        var walker = new ReachabilityWalker(schemas);
        walker.VisitDocument(document);

        var orphans = schemas.Keys
            .Where(name => !walker.Reachable.Contains(name))
            .ToList();

        foreach (var orphan in orphans)
        {
            schemas.Remove(orphan);
        }

        return Task.CompletedTask;
    }

    private sealed class ReachabilityWalker(IDictionary<string, IOpenApiSchema> schemas)
    {
        private readonly HashSet<string> reachable = new(StringComparer.Ordinal);

        private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);

        private readonly Queue<string> queue = new();

        internal HashSet<string> Reachable => reachable;

        internal void VisitDocument(OpenApiDocument document)
        {
            if (document.Paths is not null)
            {
                foreach (var pathItem in document.Paths.Values)
                {
                    VisitPathItem(pathItem);
                }
            }

            while (queue.Count > 0)
            {
                if (schemas.TryGetValue(queue.Dequeue(), out var schema))
                {
                    VisitSchema(schema);
                }
            }
        }

        private void VisitPathItem(IOpenApiPathItem? pathItem)
        {
            if (pathItem is null || !visited.Add(pathItem))
            {
                return;
            }

            VisitParameters(pathItem.Parameters);

            if (pathItem.Operations is null)
            {
                return;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                VisitOperation(operation);
            }
        }

        private void VisitOperation(OpenApiOperation? operation)
        {
            if (operation is null)
            {
                return;
            }

            VisitParameters(operation.Parameters);
            VisitContent(operation.RequestBody?.Content);

            if (operation.Responses is not null)
            {
                foreach (var response in operation.Responses.Values)
                {
                    VisitContent(response?.Content);
                    VisitHeaders(response?.Headers);
                }
            }

            if (operation.Callbacks is null)
            {
                return;
            }

            foreach (var callback in operation.Callbacks.Values)
            {
                if (callback?.PathItems is null)
                {
                    continue;
                }

                foreach (var pathItem in callback.PathItems.Values)
                {
                    VisitPathItem(pathItem);
                }
            }
        }

        private void VisitParameters(IList<IOpenApiParameter>? parameters)
        {
            if (parameters is null)
            {
                return;
            }

            foreach (var parameter in parameters)
            {
                if (parameter is null)
                {
                    continue;
                }

                VisitSchema(parameter.Schema);
                VisitContent(parameter.Content);
            }
        }

        private void VisitHeaders(IDictionary<string, IOpenApiHeader>? headers)
        {
            if (headers is null)
            {
                return;
            }

            foreach (var header in headers.Values)
            {
                if (header is null)
                {
                    continue;
                }

                VisitSchema(header.Schema);
                VisitContent(header.Content);
            }
        }

        private void VisitContent(IDictionary<string, OpenApiMediaType>? content)
        {
            if (content is null)
            {
                return;
            }

            foreach (var mediaType in content.Values)
            {
                if (mediaType is null)
                {
                    continue;
                }

                VisitSchema(mediaType.Schema);

                if (mediaType.Encoding is null)
                {
                    continue;
                }

                foreach (var encoding in mediaType.Encoding.Values)
                {
                    VisitHeaders(encoding?.Headers);
                }
            }
        }

        private void VisitSchema(IOpenApiSchema? schema)
        {
            if (schema is null)
            {
                return;
            }

            if (schema is OpenApiSchemaReference reference)
            {
                var id = reference.Reference?.Id;

                if (!string.IsNullOrEmpty(id) && reachable.Add(id))
                {
                    queue.Enqueue(id);
                }

                return;
            }

            if (!visited.Add(schema))
            {
                return;
            }

            VisitSchema(schema.Items);
            VisitSchema(schema.Not);
            VisitSchema(schema.AdditionalProperties);

            VisitSchemas(schema.AllOf);
            VisitSchemas(schema.AnyOf);
            VisitSchemas(schema.OneOf);

            VisitSchemaMap(schema.Properties);
            VisitSchemaMap(schema.PatternProperties);
            VisitSchemaMap(schema.Definitions);

            if (schema.Discriminator?.Mapping is null)
            {
                return;
            }

            foreach (var target in schema.Discriminator.Mapping.Values)
            {
                VisitSchema(target);
            }
        }

        private void VisitSchemas(IList<IOpenApiSchema>? items)
        {
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                VisitSchema(item);
            }
        }

        private void VisitSchemaMap(IDictionary<string, IOpenApiSchema>? items)
        {
            if (items is null)
            {
                return;
            }

            foreach (var item in items.Values)
            {
                VisitSchema(item);
            }
        }
    }
}
