using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace SqlFroega.Configuration;

internal static class DeepLinkProtocolRegistrar
{
    private const string ProtocolDescription = "URL:SqlFroega Protocol";

    public static bool EnsureRegistered()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return false;

        var escapedExecutablePath = executablePath.Replace("\"", "\"\"");
        var commandValue = $"\"{escapedExecutablePath}\" \"%1\"";

        const string baseKeyPath = @"Software\Classes\sqlfroega";
        const string commandKeyPath = @"Software\Classes\sqlfroega\shell\open\command";

        try
        {
            using var baseKey = Registry.CurrentUser.CreateSubKey(baseKeyPath, writable: true);
            if (baseKey is null)
                return false;

            SetIfDifferent(baseKey, string.Empty, ProtocolDescription);
            SetIfDifferent(baseKey, "URL Protocol", string.Empty);

            using (var iconKey = baseKey.CreateSubKey("DefaultIcon", writable: true))
            {
                if (iconKey is not null)
                {
                    SetIfDifferent(iconKey, string.Empty, $"{executablePath},1");
                }
            }

            using var commandKey = Registry.CurrentUser.CreateSubKey(commandKeyPath, writable: true);
            if (commandKey is null)
                return false;

            SetIfDifferent(commandKey, string.Empty, commandValue);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Deep link registration failed: {ex.Message}");
            return false;
        }
    }

    private static void SetIfDifferent(RegistryKey key, string name, string expectedValue)
    {
        var current = key.GetValue(name) as string;
        if (!string.Equals(current, expectedValue, StringComparison.Ordinal))
        {
            key.SetValue(name, expectedValue, RegistryValueKind.String);
        }
    }
}
