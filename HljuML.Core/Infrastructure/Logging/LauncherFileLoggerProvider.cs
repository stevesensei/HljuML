using System.Diagnostics;
using System.Text;
using HljuML.Core.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace HljuML.Core.Infrastructure.Logging;

public sealed class LauncherFileLoggerProvider : ILoggerProvider
{
    private readonly object _sync = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var now = DateTimeOffset.Now;
        var text = $"{now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {category}: {message}{Environment.NewLine}";
        if (exception is not null) text += exception + Environment.NewLine;
        lock (_sync)
        {
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
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) owner.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
