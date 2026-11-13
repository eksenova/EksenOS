using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.OpenApi;

/// <summary>
/// Describes controller tags from the controller type: <see cref="DescriptionAttribute"/> becomes the tag
/// description and <see cref="DisplayNameAttribute"/> the <c>x-displayName</c> shown in reference UIs.
/// Controllers that share a tag through <c>[Tags]</c> contribute the first description found. Tags are then
/// sorted by display name so the sidebar reads alphabetically rather than in registration order.
/// </summary>
public sealed class ControllerTagMetadataDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// The vendor extension Scalar and Redoc read as a tag's human-readable name.
    /// </summary>
    public const string DisplayNameExtension = "x-displayName";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = CollectMetadata(context);
        document.Tags ??= new HashSet<OpenApiTag>();

        foreach (var name in metadata.Keys.Where(name => document.Tags.All(tag => tag.Name != name)))
        {
            document.Tags.Add(new OpenApiTag { Name = name });
        }

        foreach (var tag in document.Tags)
        {
            if (tag.Name is null || !metadata.TryGetValue(tag.Name, out var controller))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(tag.Description) && !string.IsNullOrWhiteSpace(controller.Description))
            {
                tag.Description = controller.Description;
            }

            if (!string.IsNullOrWhiteSpace(controller.DisplayName))
            {
                tag.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
                tag.Extensions[DisplayNameExtension] = new JsonNodeExtension(JsonValue.Create(controller.DisplayName));
            }
        }

        document.Tags = new SortedSet<OpenApiTag>(document.Tags, TagOrderComparer.Instance);

        return Task.CompletedTask;
    }

    private static Dictionary<string, TagMetadata> CollectMetadata(OpenApiDocumentTransformerContext context)
    {
        var metadata = new Dictionary<string, TagMetadata>(StringComparer.Ordinal);

        foreach (var description in context.DescriptionGroups.SelectMany(group => group.Items))
        {
            if (description.ActionDescriptor is not ControllerActionDescriptor action)
            {
                continue;
            }

            var type = action.ControllerTypeInfo;
            var found = new TagMetadata(
                type.GetCustomAttribute<DisplayNameAttribute>(inherit: true)?.DisplayName,
                type.GetCustomAttribute<DescriptionAttribute>(inherit: true)?.Description);

            foreach (var name in TagNamesOf(action))
            {
                if (!metadata.TryGetValue(name, out var existing))
                {
                    metadata[name] = found;
                    continue;
                }

                metadata[name] = new TagMetadata(
                    existing.DisplayName ?? found.DisplayName,
                    existing.Description ?? found.Description);
            }
        }

        return metadata;
    }

    private static IEnumerable<string> TagNamesOf(ControllerActionDescriptor action)
    {
        var tags = action.EndpointMetadata.OfType<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()
            .SelectMany(tags => tags.Tags)
            .ToList();

        return tags.Count > 0 ? tags : [action.ControllerName];
    }

    private sealed class TagOrderComparer : IComparer<OpenApiTag>
    {
        internal static readonly TagOrderComparer Instance = new();

        public int Compare(OpenApiTag? x, OpenApiTag? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var byDisplayName = StringComparer.CurrentCultureIgnoreCase.Compare(DisplayNameOf(x), DisplayNameOf(y));

            return byDisplayName != 0 ? byDisplayName : StringComparer.Ordinal.Compare(x.Name, y.Name);
        }
    }

    private static string DisplayNameOf(OpenApiTag tag)
    {
        if (tag.Extensions is not null
            && tag.Extensions.TryGetValue(DisplayNameExtension, out var extension)
            && extension is JsonNodeExtension { Node: JsonValue value }
            && value.TryGetValue<string>(out var displayName))
        {
            return displayName;
        }

        return tag.Name ?? string.Empty;
    }

    private sealed record TagMetadata(string? DisplayName, string? Description);
}
