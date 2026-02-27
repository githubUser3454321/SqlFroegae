using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;



namespace SqlFroega
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private bool _initialized;

        public MainWindow()
        {
            InitializeComponent();
            //Activated += MainWindow_Activated;

        }
        /// <summary>
        /// Testing....
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_initialized) return;
            _initialized = true;

            var repo = App.Services.GetRequiredService<IScriptRepository>();

            var filters = new ScriptSearchFilters(
                Scope: null,
                CustomerId: null,
                Module: null,
                Tags: null
            );

            var results = await repo.SearchAsync("sys", filters, take: 50, skip: 0);

            System.Diagnostics.Debug.WriteLine($"Found: {results.Count}");
            foreach (var r in results)
                System.Diagnostics.Debug.WriteLine($"{r.Name} - {r.Key}");
        }
    }
}
