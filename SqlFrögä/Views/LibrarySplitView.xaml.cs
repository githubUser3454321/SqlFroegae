using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SqlFroega.Application.Models;
using SqlFroega.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace SqlFroega.Views;

public sealed partial class LibrarySplitView : Page
{
    private int? _pendingScriptNumberId;

    public LibrarySplitView()
    {
        InitializeComponent();

        _resizeDebounceTimer = DispatcherQueue.CreateTimer();
        _resizeDebounceTimer.Interval = TimeSpan.FromMilliseconds(250);
        _resizeDebounceTimer.IsRepeating = false;
        _resizeDebounceTimer.Tick += ResizeDebounceTimer_Tick;

        // Frame in VM registrieren, damit VM rechts navigieren kann.
        VM.AttachDetailFrame(DetailFrame);

        // Startzustand rechts: leere Auswahlmaske mit Hinweis
        DetailFrame.Navigate(typeof(ScriptSelectionPlaceholderView));

        Loaded += LibrarySplitView_Loaded;
    }

    private LibrarySplitViewModel VM => (LibrarySplitViewModel)DataContext;

    private readonly DispatcherQueueTimer _resizeDebounceTimer;
    private double _pendingResultsViewportHeight;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is int scriptNumberId && scriptNumberId > 0)
        {
            _pendingScriptNumberId = scriptNumberId;
        }
    }

    private async void LibrarySplitView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LibrarySplitView_Loaded;
        await VM.RefreshCatalogCommand.ExecuteAsync(null);
        await VM.RestoreWorkspaceStateForCurrentUserAsync();

        if (_pendingScriptNumberId is int scriptNumberId)
        {
            _pendingScriptNumberId = null;
            await VM.OpenByNumberIdAsync(scriptNumberId);
            return;
        }

        RestoreDetailViewFromSavedState();
    }

    private async void QueryTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            await VM.SearchCommand.ExecuteAsync(null);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid scriptId })
            return;

        var shouldDelete = await ConfirmDeleteAsync();
        if (!shouldDelete)
            return;

        await VM.DeleteCommand.ExecuteAsync(scriptId);
    }

    private void ResultsList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Height <= 0)
            return;

        _pendingResultsViewportHeight = e.NewSize.Height;
        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private async void ResizeDebounceTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        await VM.UpdateViewportHeightAsync(_pendingResultsViewportHeight);
    }


    private void CustomerMappingAdmin_Click(object sender, RoutedEventArgs e)
    {
        VM.OpenCustomerMappingAdminCommand.Execute(null);
    }

    private void ModuleAdmin_Click(object sender, RoutedEventArgs e)
    {
        VM.OpenModuleAdminCommand.Execute(null);
    }


    private void UserManagementAdmin_Click(object sender, RoutedEventArgs e)
    {
        VM.OpenUserManagementAdminCommand.Execute(null);
    }
    private async Task<bool> ConfirmDeleteAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Script wirklich löschen?",
            Content = "Diese Aktion entfernt das Script dauerhaft.",
            PrimaryButtonText = "Löschen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };
        dialog.CloseButtonStyle = App.Current.Resources["LightBluePrimaryDialogButton"] as Style;
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async void OpenSpotlightQueryStudio_Click(object sender, RoutedEventArgs e)
    {
        var spotlightView = new SpotlightQueryStudioView();
        spotlightView.InitializeFrom(VM);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Spotlight Query Studio",
            PrimaryButtonText = "Suche starten",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
            FullSizeDesired = true,
            Content = spotlightView
        };

        dialog.PrimaryButtonStyle = App.Current.Resources["LightBluePrimaryDialogButton"] as Style;
        dialog.CloseButtonStyle = App.Current.Resources["LightBluePrimaryDialogButton"] as Style;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        spotlightView.ApplyTo(VM);
        await VM.SearchCommand.ExecuteAsync(null);
    }

    public async Task SaveWorkspaceStateAsync()
    {
        var (target, scriptId) = ResolveCurrentDetailTarget();
        await VM.SaveWorkspaceStateForCurrentUserAsync(target, scriptId);
    }

    private void RestoreDetailViewFromSavedState()
    {
        var state = VM.LastLoadedWorkspaceState;
        if (state is null)
            return;

        switch (state.DetailTarget)
        {
            case WorkspaceDetailTarget.ScriptItem when state.DetailScriptId is Guid scriptId:
                DetailFrame.Navigate(typeof(ScriptItemView), scriptId);
                break;
            case WorkspaceDetailTarget.CustomerMappingAdmin:
                DetailFrame.Navigate(typeof(CustomerMappingAdminView));
                break;
            case WorkspaceDetailTarget.ModuleAdmin:
                DetailFrame.Navigate(typeof(ModuleAdminView));
                break;
            case WorkspaceDetailTarget.UserManagementAdmin:
                DetailFrame.Navigate(typeof(UserManagementAdminView));
                break;
            default:
                DetailFrame.Navigate(typeof(ScriptSelectionPlaceholderView));
                break;
        }
    }

    private (WorkspaceDetailTarget target, Guid? scriptId) ResolveCurrentDetailTarget()
    {
        return DetailFrame.Content switch
        {
            CustomerMappingAdminView => (WorkspaceDetailTarget.CustomerMappingAdmin, null),
            ModuleAdminView => (WorkspaceDetailTarget.ModuleAdmin, null),
            UserManagementAdminView => (WorkspaceDetailTarget.UserManagementAdmin, null),
            ScriptItemView => (WorkspaceDetailTarget.ScriptItem, VM.CurrentDetailScriptId),
            _ => (WorkspaceDetailTarget.Placeholder, null)
        };
    }
}
