namespace Eksen.Templating.Pdf;

public interface IHtmlPdfRenderer
{
    Task<byte[]> ConvertAsync(
        string html,
        CancellationToken cancellationToken = default);
}