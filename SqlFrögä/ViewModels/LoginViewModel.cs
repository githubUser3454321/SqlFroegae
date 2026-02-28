using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;

namespace SqlFroega.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IUserRepository _userRepository;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public event Action? LoginSucceeded;

    public LoginViewModel()
    {
        _userRepository = App.Services.GetRequiredService<IUserRepository>();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            Error = "Bitte Benutzernamen eingeben.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            Error = "Bitte Passwort eingeben.";
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            var user = await _userRepository.FindActiveByCredentialsAsync(Username, Password);

            if (user is null)
            {
                Error = "Anmeldung fehlgeschlagen: Benutzername oder Passwort ungültig bzw. Benutzer deaktiviert.";
                return;
            }

            App.CurrentUser = user;
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
