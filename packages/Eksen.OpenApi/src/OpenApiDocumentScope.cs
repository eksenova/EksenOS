using Eksen.OpenApi.Internal;
using Microsoft.AspNetCore.OpenApi;

namespace Eksen.OpenApi;

/// <summary>
/// Whether an OpenAPI document is published to everyone or only to operators.
/// </summary>
public enum OpenApiDocumentScope
{
    /// <summary>
    /// Excludes internal endpoints and every model only they use.
    /// </summary>
    Public,

    /// <summary>
    /// Includes every endpoint, internal ones included.
    /// </summary>
    Internal
}

/// <summary>
/// Registration helpers for scoped OpenAPI documents.
/// </summary>
public static class EksenOpenApiOptionsExtensions
{
    /// <summary>
    /// Configures the document for <paramref name="scope"/> and adds the Eksen document hygiene transformers:
    /// internal-API filtering, controller tag metadata, standard parameter and property descriptions, and the
    /// final orphan schema and tag sweeps. Call it after adding any document transformer that adds or removes
    /// operations, so the sweeps see the final set of paths.
    /// </summary>
    public static OpenApiOptions UseEksenDocumentScope(this OpenApiOptions options, OpenApiDocumentScope scope)
    {
        ArgumentNullException.ThrowIfNull(options);

        var include = options.ShouldInclude;
        options.ShouldInclude = scope == OpenApiDocumentScope.Public
            ? description => !InternalApi.IsInternal(description) && include(description)
            : include;

        options.AddOperationTransformer<InternalApiOperationMarkerTransformer>();
        options.AddSchemaTransformer<InternalApiSchemaMarkerTransformer>();
        options.AddOperationTransformer<StandardParameterDescriptionTransformer>();
        options.AddSchemaTransformer<StandardPropertyDescriptionTransformer>();
        options.AddDocumentTransformer(new InternalApiDocumentTransformer(scope));
        options.AddDocumentTransformer<ControllerTagMetadataDocumentTransformer>();
        options.AddDocumentTransformer<OrphanSchemaDocumentTransformer>();
        options.AddDocumentTransformer<OrphanTagDocumentTransformer>();

        return options;
    }
}
