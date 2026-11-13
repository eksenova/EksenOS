using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Eksen.OpenApi;

/// <summary>
/// One place for the wording of parameters and properties that recur across an API, such as paging
/// (<c>skip</c>, <c>take</c>), free-text search and common filters. Names match case-insensitively. A
/// description written on the member itself always wins; the catalogue only fills blanks.
/// </summary>
public sealed class OpenApiStandardDescriptions
{
    /// <summary>
    /// Descriptions keyed by query, route, header or form parameter name.
    /// </summary>
    public IDictionary<string, string> Parameters { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Descriptions keyed by JSON property name, applied to request and response models.
    /// </summary>
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds the same description for a parameter and a property of that name.
    /// </summary>
    public OpenApiStandardDescriptions Add(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Parameters[name] = description;
        Properties[name] = description;

        return this;
    }
}

/// <summary>
/// Fills empty parameter descriptions from <see cref="OpenApiStandardDescriptions.Parameters"/>.
/// </summary>
public sealed class StandardParameterDescriptionTransformer(IOptions<OpenApiStandardDescriptions> options) : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            if (string.IsNullOrWhiteSpace(parameter.Description)
                && parameter.Name is not null
                && options.Value.Parameters.TryGetValue(parameter.Name, out var description))
            {
                parameter.Description = description;
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Fills empty property descriptions from <see cref="OpenApiStandardDescriptions.Properties"/>.
/// </summary>
public sealed class StandardPropertyDescriptionTransformer(IOptions<OpenApiStandardDescriptions> options) : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Properties is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (name, property) in schema.Properties)
        {
            if (property is OpenApiSchema concrete
                && string.IsNullOrWhiteSpace(concrete.Description)
                && options.Value.Properties.TryGetValue(name, out var description))
            {
                concrete.Description = description;
            }
        }

        return Task.CompletedTask;
    }
}
