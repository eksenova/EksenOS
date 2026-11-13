using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi;

/// <summary>
/// Removes document-level tags that no remaining operation uses.
/// </summary>
public sealed class OrphanTagDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (document.Tags is null || document.Tags.Count == 0)
        {
            return Task.CompletedTask;
        }

        var referencedTagNames = CollectReferencedTagNames(document);

        document.Tags = new HashSet<OpenApiTag>(
            document.Tags.Where(tag =>
                tag.Name is null
                || referencedTagNames.Contains(tag.Name)));

        return Task.CompletedTask;
    }

    private static HashSet<string> CollectReferencedTagNames(OpenApiDocument document)
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        if (document.Paths is null)
        {
            return referenced;
        }

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                if (operation.Tags is null)
                {
                    continue;
                }

                foreach (var tag in operation.Tags)
                {
                    if (tag.Name is not null)
                    {
                        referenced.Add(tag.Name);
                    }
                }
            }
        }

        return referenced;
    }
}
