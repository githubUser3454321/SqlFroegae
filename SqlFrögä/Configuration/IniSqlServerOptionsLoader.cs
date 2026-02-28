using SqlFroega.Infrastructure.Persistence.SqlServer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SqlFroega.Configuration;

internal static class IniSqlServerOptionsLoader
{
    public static SqlServerOptions Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Es wurde kein INI-Dateipfad angegeben.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Die angegebene INI-Datei wurde nicht gefunden.", path);
        }

        var values = Parse(path);

        if (!values.TryGetValue("ConnectionString", out var connectionString) || string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidDataException("Der Schlüssel 'ConnectionString' fehlt oder ist leer (Sektion [SqlServer]).");
        }

        return new SqlServerOptions
        {
            ConnectionString = connectionString,
            ScriptsTable = GetString(values, "ScriptsTable", "dbo.SqlScripts"),
            CustomersTable = GetString(values, "CustomersTable", "dbo.Customers"),
            ScriptObjectRefsTable = GetString(values, "ScriptObjectRefsTable", "dbo.ScriptObjectRefs"),
            ModulesTable = GetString(values, "ModulesTable", "dbo.Modules"),
            UseFullTextSearch = GetBool(values, "UseFullTextSearch", false),
            JoinCustomers = GetBool(values, "JoinCustomers", true),
            EnableSoftDelete = GetBool(values, "EnableSoftDelete", true)
        };
    }

    private static Dictionary<string, string> Parse(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inSqlServerSection = false;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var section = line[1..^1].Trim();
                inSqlServerSection = section.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSqlServerSection)
            {
                continue;
            }

            var splitIndex = line.IndexOf('=');
            if (splitIndex <= 0)
            {
                continue;
            }

            var key = line[..splitIndex].Trim();
            var value = line[(splitIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }

    private static string GetString(Dictionary<string, string> values, string key, string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (bool.TryParse(value, out var parsedBool))
        {
            return parsedBool;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            return parsedInt != 0;
        }

        return fallback;
    }
}
