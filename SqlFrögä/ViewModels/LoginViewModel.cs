using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using System;
using System.Threading.Tasks;

namespace SqlFroega.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IUserRepository _userRepository;
    private readonly IScriptRepository _scriptRepository;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _staySignedIn;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public event Action? LoginSucceeded;

    public LoginViewModel()
    {
        _userRepository = App.Services.GetRequiredService<IUserRepository>();
        _scriptRepository = App.Services.GetRequiredService<IScriptRepository>();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            Error = "Bitte Benutzernamen eingeben.";
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            SqlFroega.Application.Models.UserAccount? user;

            if (string.IsNullOrWhiteSpace(Password))
            {
                user = await _userRepository.FindActiveByRememberedDeviceAsync(Username);

                if (user is null)
                {
                    Error = "Bitte Passwort eingeben oder auf diesem Gerät angemeldet bleiben aktivieren.";
                    return;
                }
            }
            else
            {
                user = await _userRepository.FindActiveByCredentialsAsync(Username, Password);

                if (user is null)
                {
                    Error = "Anmeldung fehlgeschlagen: Benutzername oder Passwort ungültig bzw. Benutzer deaktiviert.";
                    return;
                }

                await _userRepository.ClearRememberedDeviceAsync();

                if (StaySignedIn)
                {
                    await _userRepository.RememberDeviceAsync(user.Id);
                }
            }

            App.CurrentUser = user;
            await _scriptRepository.ClearEditLocksAsync();
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
