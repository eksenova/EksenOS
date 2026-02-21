using System.Dynamic;

namespace Eksen.Templating.Html;

public interface ITemplateHtmlRenderer
{
    Task<string> RenderTemplateAsync<TModel>(
        string templateKey,
        TModel model,
        ExpandoObject? viewBag = null,
        CancellationToken cancellationToken = default);
}