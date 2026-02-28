using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Infrastructure.Parsing;
using SqlFroega.Infrastructure.Persistence;
using SqlFroega.Infrastructure.Persistence.SqlServer;
using System;

namespace SqlFroega;

public partial class App : Microsoft.UI.Xaml.Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Window? MainWindow { get; private set; }
    public static UserAccount? CurrentUser { get; set; }

    public App()
    {
        InitializeComponent();

        var services = new ServiceCollection();
        services.AddOptions();

        services.Configure<SqlServerOptions>(cfg =>
        {
            cfg.ConnectionString =
                "Server=localhost\\SQLEXPRESS;Database=SqlFroega;Trusted_Connection=True;TrustServerCertificate=True;";
            cfg.ScriptsTable = "dbo.SqlScripts";
            cfg.CustomersTable = "dbo.Customers";
            cfg.UseFullTextSearch = false;
            cfg.ModulesTable = "dbo.Modules";
            cfg.JoinCustomers = true;
            cfg.EnableSoftDelete = true;
        });

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IScriptRepository, ScriptRepository>();
        services.AddScoped<ICustomerMappingRepository, CustomerMappingRepository>();
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        services.AddScoped<ISqlCustomerRenderService, SqlCustomerRenderService>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
