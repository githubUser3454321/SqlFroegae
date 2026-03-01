using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Configuration;
using SqlFroega.Infrastructure.Parsing;
using SqlFroega.Infrastructure.Persistence;
using SqlFroega.Infrastructure.Persistence.SqlServer;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SqlFroega;

public partial class App : Microsoft.UI.Xaml.Application
{
    private static readonly string[] SupportedSchemes = ["sqlfroega", "sqlfrögä"];
    private static int? _pendingScriptNumberId;

    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }
    public static UserAccount? CurrentUser { get; set; }
    public static bool ServicesReady { get; private set; }

    public App()
    {
        InitializeComponent();
        AppInstance.GetCurrent().Activated += OnAppActivated;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        CaptureDeepLinkFromCommandLine();
        EnsureMainWindow();
        _ = InitializeServicesAsync();
    }

    private void OnAppActivated(object? sender, AppActivationArguments args)
    {
        if (args.Kind == ExtendedActivationKind.Protocol && args.Data is IProtocolActivatedEventArgs protocolArgs)
        {
            CaptureDeepLink(protocolArgs.Uri);
        }

        EnsureMainWindow();

        if (!ServicesReady)
        {
            _ = InitializeServicesAsync();
            return;
        }

        if (MainWindow is MainWindow window)
        {
            _ = window.TryNavigateToPendingScriptAsync();
        }
    }

    public static int? ConsumePendingScriptNumberId()
    {
        var pendingScriptNumberId = _pendingScriptNumberId;
        _pendingScriptNumberId = null;
        return pendingScriptNumberId;
    }

    private void EnsureMainWindow()
    {
        if (MainWindow is not null)
        {
            MainWindow.Activate();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private void CaptureDeepLinkFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args.Skip(1))
        {
            if (!TryParseDeepLink(arg, out var numberId))
                continue;

            _pendingScriptNumberId = numberId;
            break;
        }
    }

    private void CaptureDeepLink(Uri? uri)
    {
        if (uri is null)
            return;

        if (TryParseDeepLink(uri.OriginalString, out var numberId))
            _pendingScriptNumberId = numberId;
    }

    private static bool TryParseDeepLink(string rawValue, out int numberId)
    {
        numberId = 0;
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        var normalized = rawValue.Trim();
        const string umlautPrefix = "sqlFrögä://scripts/";
        if (normalized.StartsWith(umlautPrefix, StringComparison.OrdinalIgnoreCase))
            return int.TryParse(normalized[umlautPrefix.Length..], out numberId) && numberId > 0;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return false;

        if (!SupportedSchemes.Any(scheme => string.Equals(uri.Scheme, scheme, StringComparison.OrdinalIgnoreCase)))
            return false;

        var path = uri.AbsolutePath.Trim('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return false;

        if (segments.Length == 1 && string.Equals(uri.Host, "scripts", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(segments[0], out numberId) && numberId > 0;

        if (segments.Length == 2 && string.Equals(segments[0], "scripts", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(segments[1], out numberId) && numberId > 0;

        return false;
    }

    private async Task InitializeServicesAsync()
    {
        if (ServicesReady)
            return;

        try
        {
            var sqlOptions = await ResolveSqlServerOptionsAsync();
            Services = BuildServiceProvider(sqlOptions);
            ServicesReady = true;

            if (MainWindow is MainWindow window)
            {
                await window.TryStartNavigationAsync();
            }
        }
        catch (Exception ex)
        {
            await ShowStartupErrorDialogAsync($"Die Konfiguration konnte nicht geladen werden:\n{ex.Message}");
            MainWindow?.Close();
        }
    }

    private static IServiceProvider BuildServiceProvider(SqlServerOptions sqlOptions)
    {
        var services = new ServiceCollection();
        services.AddOptions();

        services.Configure<SqlServerOptions>(cfg =>
        {
            cfg.ConnectionString = sqlOptions.ConnectionString;
            cfg.ScriptsTable = sqlOptions.ScriptsTable;
            cfg.CustomersTable = sqlOptions.CustomersTable;
            cfg.ScriptObjectRefsTable = sqlOptions.ScriptObjectRefsTable;
            cfg.UseFullTextSearch = sqlOptions.UseFullTextSearch;
            cfg.ModulesTable = sqlOptions.ModulesTable;
            cfg.JoinCustomers = sqlOptions.JoinCustomers;
            cfg.EnableSoftDelete = sqlOptions.EnableSoftDelete;
        });

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IHostIdentityProvider, HostIdentityProvider>();
        services.AddScoped<IScriptRepository, ScriptRepository>();
        services.AddScoped<ICustomerMappingRepository, CustomerMappingRepository>();
        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddScoped<ISqlCustomerRenderService, SqlCustomerRenderService>();

        return services.BuildServiceProvider();
    }

    private async Task<SqlServerOptions> ResolveSqlServerOptionsAsync()
    {
        var appDataPath = GetDefaultConfigPath();

        foreach (var candidate in GetCliConfigCandidates())
        {
            try
            {
                return IniSqlServerOptionsLoader.Load(candidate);
            }
            catch (Exception ex)
            {
                await ShowStartupErrorDialogAsync($"Konfiguration aus Programmargument konnte nicht geladen werden:\n{candidate}\n\n{ex.Message}");
            }
        }

        if (File.Exists(appDataPath))
        {
            try
            {
                return IniSqlServerOptionsLoader.Load(appDataPath);
            }
            catch (Exception ex)
            {
                await ShowStartupErrorDialogAsync($"Konfiguration unter '%AppData%/SqlFroega/config.ini' ist ungültig:\n{ex.Message}");
            }
        }

        while (true)
        {
            var pickedPath = await PickIniFileAsync();
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                throw new InvalidOperationException("Keine Konfigurationsdatei ausgewählt. Die Anwendung wird beendet.");
            }

            try
            {
                return IniSqlServerOptionsLoader.Load(pickedPath);
            }
            catch (Exception ex)
            {
                await ShowStartupErrorDialogAsync($"Die ausgewählte INI-Datei ist ungültig:\n{ex.Message}");
            }
        }
    }

    private static string[] GetCliConfigCandidates()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length <= 1)
        {
            return [];
        }

        var candidates = new System.Collections.Generic.List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(arg[9..].Trim('"'));
                continue;
            }

            if (arg.Equals("--config", StringComparison.OrdinalIgnoreCase) || arg.Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    candidates.Add(args[++i].Trim('"'));
                }

                continue;
            }

            if (arg.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(arg.Trim('"'));
            }
        }

        return [.. candidates];
    }

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "SqlFroega", "config.ini");
    }

    private async Task<string?> PickIniFileAsync()
    {
        if (MainWindow is null)
        {
            return null;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".ini");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.ViewMode = PickerViewMode.List;

        var hWnd = WindowNative.GetWindowHandle(MainWindow);
        InitializeWithWindow.Initialize(picker, hWnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task ShowStartupErrorDialogAsync(string message)
    {
        if (MainWindow is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Konfigurationsfehler",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = MainWindow.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
