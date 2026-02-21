using Eksen.Templating.Html;
using Eksen.Templating.Pdf;
using RazorLight;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddRazorHtmlRenderer(Action<RazorLightEngineBuilder> configureRazorLight)
        {
            serviceCollection.AddSingleton<IRazorLightEngine>(_ =>
            {
                var builder = new RazorLightEngineBuilder()
                    .UseMemoryCachingProvider();

                configureRazorLight(builder);

                return builder.Build();
            });

            serviceCollection.AddSingleton<ITemplateHtmlRenderer, RazorTemplateHtmlRenderer>();

            return serviceCollection;
        }

        public IServiceCollection AddGotenbergPdfRenderer(string baseUrl)
        {
            serviceCollection.AddHttpClient<IHtmlPdfRenderer, GotenbergHtmlPdfRenderer>(httpClient =>
            {
                httpClient.BaseAddress = new Uri(baseUrl);
            });

            return serviceCollection;
        }
    }
}