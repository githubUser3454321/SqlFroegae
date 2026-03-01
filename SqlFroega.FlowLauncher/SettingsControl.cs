using System.Windows;
using System.Windows.Controls;

namespace SqlFroega.FlowLauncher;

internal sealed class SettingsControl : UserControl
{
    public SettingsControl(PluginSettings settings, Action onSave)
    {
        var panel = new StackPanel { Margin = new Thickness(12) };

        var apiBase = CreateTextSetting(panel, "API Base URL", settings.ApiBaseUrl, v => settings.ApiBaseUrl = v);
        var username = CreateTextSetting(panel, "Username", settings.Username, v => settings.Username = v);
        var password = CreateTextSetting(panel, "Password", settings.Password, v => settings.Password = v);
        var tenant = CreateTextSetting(panel, "Default Tenant Context", settings.DefaultTenantContext, v => settings.DefaultTenantContext = v);
        var customer = CreateTextSetting(panel, "Default Customer Code", settings.DefaultCustomerCode, v => settings.DefaultCustomerCode = v);
        var debugLogging = new CheckBox
        {
            Content = "Enable/Debug Logging",
            IsChecked = settings.EnableDebugLogging,
            Margin = new Thickness(0, 0, 0, 8)
        };
        debugLogging.Checked += (_, _) => settings.EnableDebugLogging = true;
        debugLogging.Unchecked += (_, _) => settings.EnableDebugLogging = false;
        panel.Children.Add(debugLogging);


        var saveButton = new Button
        {
            Content = "Save",
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 4, 14, 4)
        };

        saveButton.Click += (_, _) => onSave();
        panel.Children.Add(saveButton);

        Content = panel;
    }

    private static TextBox CreateTextSetting(Panel panel, string label, string value, Action<string> onChanged)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 2) });

        var input = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 8), MinWidth = 300 };
        input.TextChanged += (_, _) => onChanged(input.Text);
        panel.Children.Add(input);
        return input;
    }
}
