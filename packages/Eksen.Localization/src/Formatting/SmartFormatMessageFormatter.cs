using System.Dynamic;
using SmartFormat;
using SmartFormat.Core.Extensions;
using SmartFormat.Core.Settings;

namespace Eksen.Localization.Formatting;

public class SmartFormatMessageFormatter : IMessageFormatter
{
    private readonly SmartFormatter _formatter;

    public SmartFormatMessageFormatter()
    {
        var smartSettings = new SmartSettings();
        var defaultFormatter = Smart.Default;

        var sources = defaultFormatter.GetSourceExtensions()
            .Select(s =>
            {
                try
                {
                    return Activator.CreateInstance(s.GetType()) as ISource;
                }
                catch
                {
                    return null;
                }
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToArray();

        var formatters = defaultFormatter.GetFormatterExtensions()
            .Select(f =>
            {
                try
                {
                    return Activator.CreateInstance(f.GetType()) as IFormatter;
                }
                catch
                {
                    return null;
                }
            })
            .Where(x => x != null)
            .Select(x => x!)
            .ToArray();

        _formatter = new SmartFormatter(smartSettings)
            .AddExtensions(sources)
            .AddExtensions(formatters);

        SmartSettings.IsThreadSafeMode = true;
        smartSettings.StringFormatCompatibility = false;
    }

    public string FormatMessage(string message, params ICollection<FormatParameter> formatParameters)
    {
        dynamic parameters = new ExpandoObject();
        foreach (var arg in formatParameters)
        {
            var key = arg.Key;
            var value = arg.Value;

            ((IDictionary<string, object?>)parameters).Add(key, value);
        }

        return _formatter.Format(message, parameters);
    }
}