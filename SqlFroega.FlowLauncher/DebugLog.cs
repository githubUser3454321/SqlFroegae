using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SqlFroega.FlowLauncher;

internal static class DebugLog
{
    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, Stopwatch> Timers = new(StringComparer.Ordinal);
    private static string? _logFilePath;
    private static volatile bool _enabled;

    public static void Configure(string pluginDirectory, bool enabled)
    {
        _enabled = enabled;
        _logFilePath = Path.Combine(pluginDirectory, "sqlfroega-debug.log");

        if (!enabled)
        {
            return;
        }

        Write("logger", "Debug logging enabled");
    }

    public static void Write(string area, string message)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_logFilePath))
        {
            return;
        }

        var line = $"{DateTimeOffset.UtcNow:O} [t{Environment.CurrentManagedThreadId}] [{area}] {message}";
        lock (Gate)
        {
            File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    public static string Begin(string area, string operation)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var key = $"{area}:{operation}:{id}";
        Timers[key] = Stopwatch.StartNew();
        Write(area, $"BEGIN {operation} ({id})");
        return key;
    }

    public static void End(string operationKey, string details = "")
    {
        if (Timers.TryRemove(operationKey, out var watch))
        {
            watch.Stop();
            var area = operationKey.Split(':')[0];
            Write(area, $"END {operationKey} elapsed={watch.ElapsedMilliseconds}ms {details}".Trim());
            return;
        }

        Write("logger", $"END without timer: {operationKey} {details}".Trim());
    }

    public static void Error(string area, Exception ex, string context)
    {
        Write(area, $"ERROR {context}: {ex.GetType().Name}: {ex.Message}");
    }
}
