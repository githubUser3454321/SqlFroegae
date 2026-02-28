using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;

namespace SqlFroega.ViewModels;

public partial class UserManagementAdminViewModel : ObservableObject
{
    private readonly IUserRepository _userRepository;

    public ObservableCollection<UserAccount> Users { get; } = new();

    [ObservableProperty] private string _newUsername = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private bool _newIsAdmin;
    [ObservableProperty] private UserAccount? _selectedUser;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;

    public UserManagementAdminViewModel()
    {
        _userRepository = App.Services.GetRequiredService<IUserRepository>();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            var all = await _userRepository.GetAllAsync();

            Users.Clear();
            foreach (var item in all)
            {
                Users.Add(item);
            }
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

    [RelayCommand]
    private async Task AddUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            Error = "Bitte einen Benutzernamen eingeben.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            Error = "Bitte ein Passwort eingeben.";
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            await _userRepository.AddAsync(NewUsername, NewPassword, NewIsAdmin);
            NewUsername = string.Empty;
            NewPassword = string.Empty;
            NewIsAdmin = false;

            await RefreshAsync();
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

    [RelayCommand]
    private async Task DeactivateSelectedAsync()
    {
        if (SelectedUser is null)
        {
            Error = "Bitte zuerst einen Benutzer auswählen.";
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            var ok = await _userRepository.DeactivateAsync(SelectedUser.Id);
            if (!ok)
            {
                Error = "Benutzer konnte nicht deaktiviert werden.";
                return;
            }

            await RefreshAsync();
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
    [RelayCommand]
    private async Task ReactivateSelectedAsync()
    {
        if (SelectedUser is null)
        {
            Error = "Bitte zuerst einen Benutzer auswählen.";
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            var ok = await _userRepository.ReactivateAsync(SelectedUser.Id);
            if (!ok)
            {
                Error = "Benutzer konnte nicht reaktiviert werden.";
                return;
            }

            await RefreshAsync();
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
