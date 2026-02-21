using System.Text;
using Eksen.Templating.Html;

namespace Eksen.Templating.Pdf;

internal sealed class GotenbergHtmlPdfRenderer
(
    HttpClient httpClient
) : IHtmlPdfRenderer
{
    private const string CreateFromHtmlApiUrl = "/forms/chromium/convert/html";

    public async Task<byte[]> ConvertAsync(
        string html,
        CancellationToken cancellationToken = default)
    {
        var htmlContent = new StringContent(
            html,
            Encoding.UTF8,
            mediaType: "text/html");

        var content = new MultipartFormDataContent
        {
            { htmlContent, "index.html", "index.html" }
        };

        content.Add(new StringContent(content: "148mm"), name: "paperWidth");
        content.Add(new StringContent(content: "210mm"), name: "paperHeight");
        content.Add(new StringContent(content: "4mm"), name: "marginTop");
        content.Add(new StringContent(content: "2mm"), name: "marginBottom");
        content.Add(new StringContent(content: "2mm"), name: "marginLeft");
        content.Add(new StringContent(content: "2mm"), name: "marginRight");
        content.Add(new StringContent(content: "true"), name: "printBackground");
        content.Add(new StringContent(content: "false"), name: "landscape");
        content.Add(new StringContent(content: "1.0"), name: "scale");

        var response = await httpClient.PostAsync(CreateFromHtmlApiUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
