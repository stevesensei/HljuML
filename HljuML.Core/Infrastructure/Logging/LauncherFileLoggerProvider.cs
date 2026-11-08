using System.Diagnostics;
using System.Text;
using HljuML.Core.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using HljuML.Core.Infrastructure.Settings;

namespace HljuML.Core.Infrastructure.Logging;

public sealed class LauncherFileLoggerProvider(LauncherSettingsService settings) : ILoggerProvider
{
    private readonly object _sync = new();
    private readonly Queue<string> _recent = new();
    public string GetRecentText() { lock (_sync) return string.Concat(_recent); }
    private bool IsLevelEnabled(LogLevel level) => level >= settings.Value.MinimumLogLevel && level != LogLevel.None;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var now = DateTimeOffset.Now;
        var text = $"{now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {category}: {message}{Environment.NewLine}";
        if (exception is not null) text += exception + Environment.NewLine;
        lock (_sync)
        {
            _recent.Enqueue(text);
            while (_recent.Count > 1000) _recent.Dequeue();
            try
            {
                Directory.CreateDirectory(LauncherPaths.LogDirectory);
                File.AppendAllText(Path.Combine(LauncherPaths.LogDirectory, $"launcher-{now:yyyy-MM-dd}.log"),
                    text, Encoding.UTF8);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Trace.WriteLine($"无法写入启动器日志：{error.Message}{Environment.NewLine}{text}");
            }
        }
    }

    private sealed class FileLogger(LauncherFileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => owner.IsLevelEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
