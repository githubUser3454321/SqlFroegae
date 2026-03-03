using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SqlFroega.ViewModels;

public partial class UserManagementAdminViewModel : ObservableObject
{
    private readonly IUserRepository _userRepository;
    private readonly IScriptRepository _scriptRepository;

    public ObservableCollection<UserAccount> Users { get; } = new();

    [ObservableProperty] private string _newUsername = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private bool _newIsAdmin;
    [ObservableProperty] private UserAccount? _selectedUser;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _recordInUseTargetId = string.Empty;

    public UserManagementAdminViewModel()
    {
        _userRepository = App.Services.GetRequiredService<IUserRepository>();
        _scriptRepository = App.Services.GetRequiredService<IScriptRepository>();
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

    [RelayCommand]
    private async Task DeleteSelectedPermanentlyAsync()
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

            var ok = await _userRepository.DeletePermanentlyAsync(SelectedUser.Id);
            if (!ok)
            {
                Error = "Benutzer konnte nicht endgültig gelöscht werden.";
                return;
            }

            await _scriptRepository.ClearEditLocksAsync(SelectedUser.Username);
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
    private async Task ClearRecordInUseAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            var removed = await _scriptRepository.ClearAllEditLocksAsync();
            Error = removed > 0
                ? $"{removed} Bearbeitungssperre(n) aus [dbo].[RecordInUse] entfernt."
                : "Keine Bearbeitungssperren in [dbo].[RecordInUse] vorhanden.";
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
    private async Task ClearSpecificRecordInUseAsync()
    {
        if (string.IsNullOrWhiteSpace(RecordInUseTargetId))
        {
            Error = "Bitte eine NumberId oder GUID eingeben.";
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            var raw = RecordInUseTargetId.Trim();
            Guid? scriptId = null;

            if (Guid.TryParse(raw, out var parsedGuid))
            {
                scriptId = parsedGuid;
            }
            else if (int.TryParse(raw, out var numberId) && numberId > 0)
            {
                scriptId = await _scriptRepository.GetIdByNumberIdAsync(numberId);
                if (!scriptId.HasValue)
                {
                    Error = $"Kein Skript mit NumberId {numberId} gefunden.";
                    return;
                }
            }
            else
            {
                Error = "Ungültiger Wert. Bitte NumberId (z.B. 123) oder GUID angeben.";
                return;
            }

            var removed = await _scriptRepository.ForceReleaseEditLockAsync(scriptId.Value);
            Error = removed
                ? $"RecordInUse für ScriptId {scriptId.Value} wurde entfernt."
                : $"Kein RecordInUse-Eintrag für ScriptId {scriptId.Value} vorhanden.";
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