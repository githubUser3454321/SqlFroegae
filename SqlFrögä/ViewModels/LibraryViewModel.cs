using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SqlFroega.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IScriptRepository _repo;

    public ObservableCollection<ScriptListItem> Results { get; } = new();

    [ObservableProperty]
    private string _queryText = "";

    [ObservableProperty]
    private ScriptListItem? _selected;

    [ObservableProperty]
    private ScriptDetail? _selectedDetail;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _error;

    public LibraryViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        try
        {
            IsBusy = true;
            Error = null;

            var filters = new ScriptSearchFilters(
                Scope: null,
                CustomerId: null,
                Module: null,
                Tags: null
            );

            var items = await _repo.SearchAsync(QueryText, filters, take: 200, skip: 0);

            Results.Clear();
            foreach (var it in items)
                Results.Add(it);
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

    partial void OnSelectedChanged(ScriptListItem? value)
    {
        _ = LoadSelectedAsync(value);
    }

    private async Task LoadSelectedAsync(ScriptListItem? item)
    {
        if (item is null)
        {
            SelectedDetail = null;
            return;
        }

        try
        {
            IsBusy = true;
            Error = null;

            SelectedDetail = await _repo.GetByIdAsync(item.Id);
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