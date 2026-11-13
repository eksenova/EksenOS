using System.Text.Json;
using Eksen.Telemetry.OpenTelemetry.Logging;
using Eksen.TestBase;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.Telemetry.OpenTelemetry.Tests;

public class ConsoleFormatterTests : EksenUnitTestBase
{
    [Fact]
    public void The_Json_Console_Should_Not_Print_Secrets_From_State_Message_Or_Scopes()
    {
        // Arrange
        var logger = new FormattingLogger(includeScopes: true);

        // Act
        using (logger.BeginScope(new Dictionary<string, object?> { ["session_id"] = "sess-42", ["eksen.user_id"] = "u-1" }))
        {
            logger.LogWarning("Login for {password} at {url.full}", "hunter2", "/login?token=abc");
        }

        // Assert
        var line = logger.Output.ToString();
        line.ShouldNotContain("hunter2");
        line.ShouldNotContain("token=abc");
        line.ShouldNotContain("sess-42");

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        root.GetProperty("LogLevel").GetString().ShouldBe("Warning");
        root.GetProperty("Category").GetString().ShouldBe("Tests");
        root.GetProperty("Message").GetString().ShouldBe("Login for [redacted] at /login?token=[redacted]");
        var state = root.GetProperty("State");
        state.GetProperty("password").GetString().ShouldBe("[redacted]");
        state.GetProperty("url.full").GetString().ShouldBe("/login?token=[redacted]");
        state.GetProperty("{OriginalFormat}").GetString().ShouldBe("Login for {password} at {url.full}");
        var scope = root.GetProperty("Scopes")[0];
        scope.GetProperty("session_id").GetString().ShouldBe("[redacted]");
        scope.GetProperty("eksen.user_id").GetString().ShouldBe("u-1");
    }

    [Fact]
    public void The_Json_Console_Should_Redact_Exceptions_And_Keep_Typed_Values()
    {
        var logger = new FormattingLogger(includeScopes: false);

        logger.LogError(new InvalidOperationException("GET /items?access_token=SECRET"), "Failed {Count} {Ratio}", 3, 0.5);

        var line = logger.Output.ToString();
        line.ShouldNotContain("SECRET");
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        root.GetProperty("Exception").GetString().ShouldNotBeNull().ShouldContain("access_token=[redacted]");
        root.GetProperty("State").GetProperty("Count").GetInt32().ShouldBe(3);
        root.GetProperty("State").GetProperty("Ratio").GetDouble().ShouldBe(0.5);
        root.TryGetProperty("Scopes", out _).ShouldBeFalse();
    }

    /// <summary>
    /// Drives the formatter the way the console logger does, writing into <see cref="Output"/>.
    /// </summary>
    private sealed class FormattingLogger(bool includeScopes) : ILogger
    {
        private readonly LoggerExternalScopeProvider _scopes = new();

        private readonly RedactingJsonConsoleFormatter _formatter = new(new StaticOptionsMonitor(new JsonConsoleFormatterOptions
        {
            IncludeScopes = includeScopes,
            UseUtcTimestamp = true,
            JsonWriterOptions = new JsonWriterOptions { Indented = false }
        }));

        public StringWriter Output { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return _scopes.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _formatter.Write(new LogEntry<TState>(logLevel, "Tests", eventId, state, exception, formatter), _scopes, Output);
        }
    }

    private sealed class StaticOptionsMonitor(JsonConsoleFormatterOptions value) : IOptionsMonitor<JsonConsoleFormatterOptions>
    {
        public JsonConsoleFormatterOptions CurrentValue
        {
            get { return value; }
        }

        public JsonConsoleFormatterOptions Get(string? name)
        {
            return value;
        }

        public IDisposable? OnChange(Action<JsonConsoleFormatterOptions, string?> listener)
        {
            return null;
        }
    }
}
