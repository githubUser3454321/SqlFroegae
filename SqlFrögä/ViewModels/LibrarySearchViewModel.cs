using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SqlFroega.ViewModels;

public partial class LibrarySearchViewModel : ObservableObject
{
    private readonly IScriptRepository _repo;

    public ObservableCollection<ScriptListItem> Results { get; } = new();

    [ObservableProperty] private string _queryText = "";
    [ObservableProperty] private ScriptListItem? _selected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;

    public string ResultsCountText => Results.Count == 0 ? "No results" : $"{Results.Count} results";

    public LibrarySearchViewModel()
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

    [RelayCommand]
    private void OpenSelected()
    {
        if (Selected is null) return;
        NavigateToItem(Selected.Id);
    }

    [RelayCommand]
    private void New()
    {
        // Guid.Empty = "new script"
        NavigateToItem(Guid.Empty);
    }

    [RelayCommand]
    private void Edit(Guid id)
    {
        NavigateToItem(id);
    }

    [RelayCommand]
    private async Task DeleteAsync(Guid id)
    {
        // Falls DeleteAsync noch nicht implementiert ist: implementiere im Repo (siehe unten).
        try
        {
            IsBusy = true;
            Error = null;

            await _repo.DeleteAsync(id);

            // Refresh results after delete
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

    private static void NavigateToItem(Guid id)
    {
        // Einfachste Navigation: aktuelles Window -> Frame finden
        var window = App.MainWindow;
        if (window is null) return;

        var frame = FindDescendant<Frame>(window.Content as DependencyObject);
        frame?.Navigate(typeof(Views.ScriptItemView), id);
    }

    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null) return null;
        if (root is T t) return t;

        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            var result = FindDescendant<T>(child);
            if (result is not null) return result;
        }
        return null;
    }
}