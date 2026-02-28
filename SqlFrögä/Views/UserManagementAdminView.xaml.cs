using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SqlFroega.ViewModels;

namespace SqlFroega.Views;

public sealed partial class UserManagementAdminView : Page
{
    public UserManagementAdminView()
    {
        InitializeComponent();
        Loaded += UserManagementAdminView_Loaded;
    }

    private async void UserManagementAdminView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UserManagementAdminView_Loaded;

        if (DataContext is UserManagementAdminViewModel vm)
        {
            await vm.RefreshCommand.ExecuteAsync(null);
        }
    }

    private void NewPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && DataContext is UserManagementAdminViewModel vm)
        {
            vm.NewPassword = passwordBox.Password;
        }
    }
}
