using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SqlFroega.ViewModels;

public partial class ModuleAdminViewModel : ObservableObject
{
    private readonly IScriptRepository _repo;

    public ObservableCollection<string> Modules { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _selected;
    [ObservableProperty] private string _moduleName = string.Empty;

    public ModuleAdminViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
    }

    partial void OnSelectedChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ModuleName = value;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            var rows = await _repo.GetManagedModulesAsync();
            Modules.Clear();
            foreach (var module in rows)
                Modules.Add(module);
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
    private void NewModule()
    {
        Selected = null;
        ModuleName = string.Empty;
        Error = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ModuleName))
            throw new InvalidOperationException("Bitte einen Modulnamen eingeben.");

        try
        {
            IsBusy = true;
            Error = null;

            var name = ModuleName.Trim();
            await _repo.AddModuleAsync(name);
            await LoadAsync();
            Selected = name;
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
    private async Task DeleteAsync()
    {
        if (string.IsNullOrWhiteSpace(Selected))
            return;

        try
        {
            IsBusy = true;
            Error = null;

            await _repo.RemoveModuleAsync(Selected.Trim());
            NewModule();
            await LoadAsync();
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
