using Microsoft.UI.Xaml.Controls;
using SqlFroega.ViewModels;

namespace SqlFroega.Views;

public sealed partial class LoginView : Page
{
    public LoginView()
    {
        InitializeComponent();

        if (DataContext is LoginViewModel vm)
        {
            vm.LoginSucceeded += OnLoginSucceeded;
        }

        Unloaded += LoginView_Unloaded;
    }

    private void LoginView_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.LoginSucceeded -= OnLoginSucceeded;
        }

        Unloaded -= LoginView_Unloaded;
    }

    private void PasswordBox_PasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox && DataContext is LoginViewModel vm)
        {
            vm.Password = passwordBox.Password;
        }
    }

    private void OnLoginSucceeded()
    {
        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToDashboard();
        }
    }
}
