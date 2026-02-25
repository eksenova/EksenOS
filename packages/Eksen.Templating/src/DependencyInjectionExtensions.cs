using Eksen.Templating.Html;
using Eksen.Templating.Pdf;
using RazorLight;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenBuilder builder)
    {
        public IEksenBuilder AddRazorHtmlRenderer(Action<RazorLightEngineBuilder> configureAction)
        {
            var services = builder.Services;

            services.AddSingleton<IRazorLightEngine>(_ =>
            {
                var engineBuilder = new RazorLightEngineBuilder()
                    .UseMemoryCachingProvider();

                configureAction(engineBuilder);

                return engineBuilder.Build();
            });

            services.AddSingleton<ITemplateHtmlRenderer, RazorTemplateHtmlRenderer>();

            return builder;
        }

        public IEksenBuilder AddGotenbergPdfRenderer(string baseUrl)
        {
            var services = builder.Services;

            services.AddHttpClient<IHtmlPdfRenderer, GotenbergHtmlPdfRenderer>(httpClient =>
            {
                httpClient.BaseAddress = new Uri(baseUrl);
            });

            return builder;
        }
    }
}