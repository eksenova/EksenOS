using System.Dynamic;
using RazorLight;

namespace Eksen.Templating.Html;

internal sealed class RazorTemplateHtmlRenderer(IRazorLightEngine razorLightEngine)
    : ITemplateHtmlRenderer
{
    public async Task<string> RenderTemplateAsync<TModel>(string templateKey,
        TModel model,
        ExpandoObject? viewBag = null,
        CancellationToken cancellationToken = default)
    {
        var html = await razorLightEngine.CompileRenderAsync(
            templateKey,
            model,
            viewBag
        );

        return html;
    }
}