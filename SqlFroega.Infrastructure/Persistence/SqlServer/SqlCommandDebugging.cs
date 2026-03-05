using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace SqlFroega.Infrastructure.Persistence.SqlServer;

internal static class SqlCommandDebugging
{
    private static int _initialized;

    public static void Initialize(SqlServerOptions options)
    {
        if (!options.SqlDebugger)
            return;

        var path = options.SqlDebuggerPathFilename;
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("SQLDEBUGGER_PATH_FILENAME muss gesetzt sein, wenn SQLDEBUGGER=true ist.");

        SqlDebugLogFile.SetPath(path);

        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        DiagnosticListener.AllListeners.Subscribe(new SqlClientDiagnosticObserver());
    }

    private sealed class SqlClientDiagnosticObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(DiagnosticListener value)
        {
            if (value.Name.IndexOf("SqlClient", StringComparison.OrdinalIgnoreCase) >= 0)
                value.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key.IndexOf("CommandBefore", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var command = TryGetCommand(value.Value);
            if (command is null || string.IsNullOrWhiteSpace(command.CommandText))
                return;

            SqlDebugLogFile.TryAppend(command.CommandText);
        }

        public void OnError(Exception error) { }
        public void OnCompleted() { }

        private static DbCommand? TryGetCommand(object? payload)
        {
            if (payload is null)
                return null;

            var prop = payload.GetType().GetProperty("Command", BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(payload) as DbCommand;
        }
    }
}

internal static class SqlDebugLogFile
{
    private static readonly object Sync = new();
    internal const long MaxFileBytes = 10L * 1024 * 1024;
    internal const long TrimFromStartBytes = 6L * 1024 * 1024;
    private static string? _path;

    public static void SetPath(string path)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(normalizedPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("SQLDEBUGGER_PATH_FILENAME enthält kein gültiges Verzeichnis.");

            Directory.CreateDirectory(directory);
            if (!File.Exists(normalizedPath))
                using (File.Create(normalizedPath)) { }

            _path = normalizedPath;
        }
        catch
        {
            // fail-safe: logging must never break runtime code paths
        }
    }

    public static void TryAppend(string sqlCommandText)
    {
        var path = _path;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            lock (Sync)
            {
                TrimIfNeeded(path);

                var logLine = BuildLogLine(sqlCommandText);
                File.AppendAllText(path, logLine, Encoding.UTF8);
            }
        }
        catch
        {
            // fail-safe: logging must never break runtime code paths
        }
    }

    private static string BuildLogLine(string sqlCommandText)
    {
        var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        return $"[{timestamp}] {sqlCommandText}{Environment.NewLine}{Environment.NewLine}";
    }

    internal static void TrimIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxFileBytes)
            return;

        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var startPosition = Math.Min(TrimFromStartBytes, source.Length);
        source.Position = startPosition;

        using var temp = new MemoryStream();
        source.CopyTo(temp);
        File.WriteAllBytes(path, temp.ToArray());
    }
}
