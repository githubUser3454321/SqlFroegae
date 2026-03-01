using System.IO;
using System.Text.Json;

namespace SqlFroega.FlowLauncher;

internal static class SettingsPersistence
{
    private const string FileName = "sqlfroega.settings.json";

    public static PluginSettings LoadFromFile(string pluginDirectory)
    {
        try
        {
            var path = Path.Combine(pluginDirectory, FileName);
            if (!File.Exists(path))
            {
                return new PluginSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginSettings>(json) ?? new PluginSettings();
        }
        catch (Exception ex)
        {
            DebugLog.Error("settings", ex, "LoadFromFile");
            return new PluginSettings();
        }
    }

    public static void SaveToFile(string pluginDirectory, PluginSettings settings)
    {
        try
        {
            var path = Path.Combine(pluginDirectory, FileName);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            DebugLog.Error("settings", ex, "SaveToFile");
        }
    }
}
