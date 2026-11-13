using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi.Internal;

/// <summary>
/// Applies the document scope to the markers left by <see cref="InternalApiOperationMarkerTransformer"/> and
/// <see cref="InternalApiSchemaMarkerTransformer"/>. A public document loses every internal operation and every
/// polymorphic branch that points at an internal schema; an internal document keeps them.
/// The markers are removed from both, so they never reach a client.
/// </summary>
public sealed class InternalApiDocumentTransformer(OpenApiDocumentScope scope) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        FilterOperations(document);
        FilterSchemas(document);

        return Task.CompletedTask;
    }

    private void FilterOperations(OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            return;
        }

        foreach (var (path, item) in document.Paths.ToList())
        {
            if (item.Operations is null)
            {
                continue;
            }

            foreach (var (method, operation) in item.Operations.ToList())
            {
                var isInternal = operation.Extensions?.Remove(InternalApi.MarkerExtension) == true;
                if (isInternal && scope == OpenApiDocumentScope.Public)
                {
                    item.Operations.Remove(method);
                }
            }

            if (item.Operations.Count == 0)
            {
                document.Paths.Remove(path);
            }
        }
    }

    private void FilterSchemas(OpenApiDocument document)
    {
        var schemas = document.Components?.Schemas;
        if (schemas is null || schemas.Count == 0)
        {
            return;
        }

        var internalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, schema) in schemas)
        {
            if (schema is OpenApiSchema concrete && concrete.Extensions?.Remove(InternalApi.MarkerExtension) == true)
            {
                internalIds.Add(id);
            }
        }

        if (internalIds.Count == 0 || scope == OpenApiDocumentScope.Internal)
        {
            return;
        }

        // Internal schemas are not deleted here: a public operation may still reference one directly, and
        // OrphanSchemaDocumentTransformer removes every schema no remaining operation can reach.
        foreach (var schema in schemas.Values.OfType<OpenApiSchema>())
        {
            RemoveInternalBranches(schema, internalIds);
        }
    }

    private static void RemoveInternalBranches(OpenApiSchema schema, HashSet<string> internalIds)
    {
        foreach (var branches in new[] { schema.AnyOf, schema.OneOf })
        {
            if (branches is not { Count: > 0 })
            {
                continue;
            }

            foreach (var reference in branches.OfType<OpenApiSchemaReference>().Where(r => IsInternal(r, internalIds)).ToList())
            {
                branches.Remove(reference);
            }
        }

        if (schema.Discriminator?.Mapping is not { Count: > 0 } mapping)
        {
            return;
        }

        foreach (var key in mapping.Where(entry => IsInternal(entry.Value, internalIds)).Select(entry => entry.Key).ToList())
        {
            mapping.Remove(key);
        }
    }

    private static bool IsInternal(OpenApiSchemaReference reference, HashSet<string> internalIds)
    {
        var id = reference.Reference?.Id;

        return !string.IsNullOrEmpty(id) && internalIds.Contains(id);
    }
}
