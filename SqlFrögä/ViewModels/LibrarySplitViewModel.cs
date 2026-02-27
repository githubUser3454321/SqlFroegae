using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.Views;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SqlFroega.ViewModels;

public partial class LibrarySplitViewModel : ObservableObject
{
    private readonly IScriptRepository _repo;
    private Frame? _detailFrame;

    public ObservableCollection<ScriptListItem> Results { get; } = new();

    [ObservableProperty] private string _queryText = "";
    [ObservableProperty] private ScriptListItem? _selected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public string ResultsCountText => Results.Count == 0 ? "No results" : $"{Results.Count} results";

    public LibrarySplitViewModel()
    {
        _repo = App.Services.GetRequiredService<IScriptRepository>();
    }

    public void AttachDetailFrame(Frame frame) => _detailFrame = frame;

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

            OnPropertyChanged(nameof(ResultsCountText));
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
        if (value is null) return;
        NavigateDetail(value.Id);
    }

    [RelayCommand]
    private void New()
    {
        Selected = null; // optional: Selection reset
        NavigateDetail(Guid.Empty);
    }

    [RelayCommand]
    private void Edit(Guid id)
    {
        NavigateDetail(id);
    }

    [RelayCommand]
    private async Task DeleteAsync(Guid id)
    {
        try
        {
            IsBusy = true;
            Error = null;

            await _repo.DeleteAsync(id);

            // Falls gerade rechts das gelöschte Script offen war -> rechts auf "New"
            if (_detailFrame?.Content is ScriptItemView)
            {
                NavigateDetail(Guid.Empty);
            }

            await SearchAsync();
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

    private void NavigateDetail(Guid id)
    {
        if (_detailFrame is null) return;

        // Immer nur Id übergeben
        _detailFrame.Navigate(typeof(ScriptItemView), id);
    }
}