using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SqlFroega.ViewModels;

namespace SqlFroega.Views;

public sealed partial class CustomerMappingAdminView : Page
{
    public CustomerMappingAdminView()
    {
        InitializeComponent();
        Loaded += CustomerMappingAdminView_Loaded;
    }

    private CustomerMappingAdminViewModel VM => (CustomerMappingAdminViewModel)DataContext;

    private async void CustomerMappingAdminView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= CustomerMappingAdminView_Loaded;
        await VM.LoadCommand.ExecuteAsync(null);
    }
}
