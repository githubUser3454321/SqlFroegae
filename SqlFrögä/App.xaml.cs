using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using SqlFroega.Application.Abstractions;
using SqlFroega.Infrastructure.Persistence.SqlServer;
using System;


namespace SqlFrögä
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        public static IHost AppHost { get; private set; } = null!;
        public static IServiceProvider Services { get; private set; } = null!;


        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
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
                cfg.JoinCustomers = true;
            });

            services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddScoped<IScriptRepository, ScriptRepository>();

            Services = services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
            
        }
    }
}
