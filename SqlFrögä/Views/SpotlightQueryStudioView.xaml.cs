using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SqlFroega.Application.Abstractions;
using SqlFroega.Application.Models;
using SqlFroega.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SqlFroega.Views;

public sealed partial class SpotlightQueryStudioView : UserControl
{
    private static SpotlightProfileDefinition? _sessionMemoryProfile;
    private sealed record FolderTreePickerItem(Guid Id, string Name);

    private readonly ISearchProfileRepository _searchProfileRepository;
    private readonly List<SearchProfile> _loadedProfiles = new();
    private LibrarySplitViewModel? _vm;
    private Guid? _folderPickerSelectionId;

    public SpotlightQueryStudioView()
    {
        InitializeComponent();
        _searchProfileRepository = App.Services.GetRequiredService<ISearchProfileRepository>();
        SpotlightDetailFrame.Navigate(typeof(ScriptSelectionPlaceholderView));
        SpotlightDetailFrame.Navigated += SpotlightDetailFrame_Navigated;
    }

    public async Task InitializeFromAsync(LibrarySplitViewModel vm)
    {
        _vm = vm;

        CollectionCombo.ItemsSource = vm.AvailableCollections;
        SpotlightResultsList.ItemsSource = vm.Results;

        if (_sessionMemoryProfile is not null)
            ApplyDefinition(_sessionMemoryProfile);
        else
            ClearFilters();

        SpotlightDetailFrame.Navigate(typeof(ScriptSelectionPlaceholderView));
        await LoadProfilesAsync();
    }

    public async Task ApplyAndSearchAsync(LibrarySplitViewModel vm)
    {
        await vm.SearchWithSpotlightGroupsAsync(new[] { BuildRuleGroup() }, combineWithAnd: true);
        ResultsHintText.Text = $"{vm.Results.Count} Ergebnis(se). Klick auf ein Resultat speichert das Session-Memory-Profil.";
    }

    private SpotlightFilterGroup BuildRuleGroup() =>
        new(
            QueryText: NormalizePrimaryQuery(PrimaryQueryTextBox.Text),
            ScopeFilterIndex: ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex,
            MainModuleFilterText: MainModuleBox.Text,
            RelatedModuleFilterText: RelatedModuleBox.Text,
            CustomerCodeFilterText: CustomerCodeBox.Text,
            TagsFilterText: TagsBox.Text,
            ObjectFilterText: ObjectsBox.Text,
            IncludeDeleted: IncludeDeletedBox.IsChecked == true,
            SearchInHistory: SearchHistoryBox.IsChecked == true,
            FolderId: _folderPickerSelectionId,
            CollectionId: (CollectionCombo.SelectedItem as ScriptCollection)?.Id);

    private static string? NormalizePrimaryQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return query;

        return query.Replace("\r\n", "%", StringComparison.Ordinal)
                    .Replace('\n', '%')
                    .Replace('\r', '%');
    }

    private void ApplyDefinition(SpotlightProfileDefinition definition)
    {
        PrimaryQueryTextBox.Text = definition.PrimaryQueryText ?? string.Empty;
        ScopeCombo.SelectedIndex = definition.ScopeFilterIndex;
        MainModuleBox.Text = definition.MainModule ?? string.Empty;
        RelatedModuleBox.Text = definition.RelatedModule ?? string.Empty;
        CustomerCodeBox.Text = definition.CustomerCode ?? string.Empty;
        TagsBox.Text = definition.Tags ?? string.Empty;
        ObjectsBox.Text = definition.ReferencedObjects ?? string.Empty;
        IncludeDeletedBox.IsChecked = definition.IncludeDeleted;
        SearchHistoryBox.IsChecked = definition.SearchInHistory;

        _folderPickerSelectionId = definition.FolderId;
        UpdateSelectedFolderText();
        CollectionCombo.SelectedItem = (CollectionCombo.ItemsSource as IEnumerable<ScriptCollection>)?.FirstOrDefault(x => x.Id == definition.CollectionId);
    }

    private SpotlightProfileDefinition BuildDefinition() =>
        new(
            PrimaryQueryText: PrimaryQueryTextBox.Text,
            ScopeFilterIndex: ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex,
            MainModule: MainModuleBox.Text,
            RelatedModule: RelatedModuleBox.Text,
            CustomerCode: CustomerCodeBox.Text,
            Tags: TagsBox.Text,
            ReferencedObjects: ObjectsBox.Text,
            IncludeDeleted: IncludeDeletedBox.IsChecked == true,
            SearchInHistory: SearchHistoryBox.IsChecked == true,
            FolderId: _folderPickerSelectionId,
            CollectionId: (CollectionCombo.SelectedItem as ScriptCollection)?.Id);

    private void ClearFilters()
    {
        PrimaryQueryTextBox.Text = string.Empty;
        ScopeCombo.SelectedIndex = 0;
        MainModuleBox.Text = string.Empty;
        RelatedModuleBox.Text = string.Empty;
        CustomerCodeBox.Text = string.Empty;
        TagsBox.Text = string.Empty;
        ObjectsBox.Text = string.Empty;
        IncludeDeletedBox.IsChecked = false;
        SearchHistoryBox.IsChecked = false;
        _folderPickerSelectionId = null;
        UpdateSelectedFolderText();
        CollectionCombo.SelectedItem = null;
    }

    private async void OpenFolderPicker_Click(object sender, RoutedEventArgs e)
    {
        await LoadFolderTreePickerAsync();
        FolderPickerOverlay.Visibility = Visibility.Visible;
        FolderPickerOverlay.Focus(FocusState.Programmatic);
        FolderTreePicker.Focus(FocusState.Programmatic);
    }

    private void CloseFolderPicker_Click(object sender, RoutedEventArgs e)
        => HideFolderPickerOverlay();

    private void FolderPickerOverlay_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape)
            return;

        e.Handled = true;
        HideFolderPickerOverlay();
    }

    private void FolderTreePicker_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is not FolderTreePickerItem item)
            return;

        _folderPickerSelectionId = item.Id;
    }

    private void FolderTreePicker_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedNode?.Content is FolderTreePickerItem item)
            _folderPickerSelectionId = item.Id;
    }

    private void ApplyFolderSelection_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectedFolderText();
        HideFolderPickerOverlay();
    }

    private void ClearFolderSelection_Click(object sender, RoutedEventArgs e)
    {
        _folderPickerSelectionId = null;
        FolderTreePicker.SelectedNode = null;
        UpdateSelectedFolderText();
        HideFolderPickerOverlay();
    }

    private async Task LoadFolderTreePickerAsync()
    {
        if (_vm is null)
            return;

        var tree = await _vm.GetFolderTreeAsync();
        FolderTreePicker.RootNodes.Clear();

        foreach (var root in tree)
            FolderTreePicker.RootNodes.Add(BuildTreeViewNode(root));

        ExpandAndSelectFolder(FolderTreePicker.RootNodes, _folderPickerSelectionId);
    }

    private static TreeViewNode BuildTreeViewNode(ScriptFolderTreeNode folder)
    {
        var node = new TreeViewNode
        {
            Content = new FolderTreePickerItem(folder.Id, folder.Name),
            IsExpanded = true
        };

        foreach (var child in folder.Children)
            node.Children.Add(BuildTreeViewNode(child));

        return node;
    }

    private void ExpandAndSelectFolder(IList<TreeViewNode> nodes, Guid? folderId)
    {
        if (!folderId.HasValue)
            return;

        foreach (var root in nodes)
        {
            if (TrySelectFolderNode(root, folderId.Value))
                return;
        }
    }

    private bool TrySelectFolderNode(TreeViewNode node, Guid folderId)
    {
        if (node.Content is FolderTreePickerItem item && item.Id == folderId)
        {
            FolderTreePicker.SelectedNode = node;
            return true;
        }

        foreach (var child in node.Children)
        {
            if (TrySelectFolderNode(child, folderId))
            {
                node.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    private void HideFolderPickerOverlay()
        => FolderPickerOverlay.Visibility = Visibility.Collapsed;

    private void UpdateSelectedFolderText()
    {
        if (_vm is null || !_folderPickerSelectionId.HasValue)
        {
            SelectedFolderText.Text = "Alle";
            return;
        }

        var folder = _vm.AvailableFolders.FirstOrDefault(x => x.Id == _folderPickerSelectionId.Value);
        SelectedFolderText.Text = folder?.DisplayName ?? "Alle";
    }

    private async Task LoadProfilesAsync()
    {
        ProfileFeedbackText.Text = string.Empty;
        var username = App.CurrentUser?.Username;
        if (string.IsNullOrWhiteSpace(username))
            return;

        var visible = await _searchProfileRepository.GetVisibleAsync(username, includeAll: App.CurrentUser?.IsAdmin == true);
        _loadedProfiles.Clear();
        _loadedProfiles.AddRange(visible.OrderByDescending(x => x.UpdatedUtc));

        SavedProfilesCombo.ItemsSource = _loadedProfiles;
    }

    private void SpotlightResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        _sessionMemoryProfile = BuildDefinition();
        if (_vm is not null && e.ClickedItem is ScriptListItem item)
        {
            _vm.Selected = item;
            SpotlightDetailFrame.Navigate(typeof(ScriptItemView), item.Id);
        }

        ResultsHintText.Text = "Session-Memory-Profil aktualisiert. Wird beim nächsten Öffnen wiederhergestellt.";
    }

    private async void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SavedProfilesCombo.SelectedItem is not SearchProfile profile)
        {
            ProfileFeedbackText.Text = "Bitte zuerst ein Profil auswählen.";
            return;
        }

        try
        {
            var definition = JsonSerializer.Deserialize<SpotlightProfileDefinition>(profile.DefinitionJson);
            if (definition is null)
            {
                ProfileFeedbackText.Text = "Profil konnte nicht gelesen werden.";
                return;
            }

            ApplyDefinition(definition);
            _sessionMemoryProfile = definition;
            ProfileNameBox.Text = profile.Name;
            ProfileVisibilityCombo.SelectedIndex = string.Equals(profile.Visibility, "global", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            ProfileFeedbackText.Text = $"Profil '{profile.Name}' geladen.";
        }
        catch
        {
            ProfileFeedbackText.Text = "Profil konnte nicht gelesen werden.";
        }
    }

    private async void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var username = App.CurrentUser?.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            ProfileFeedbackText.Text = "Kein angemeldeter Benutzer.";
            return;
        }

        var name = (ProfileNameBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ProfileFeedbackText.Text = "Bitte einen Profilnamen eingeben.";
            return;
        }

        var visibility = ProfileVisibilityCombo.SelectedIndex == 1 && App.CurrentUser?.IsAdmin == true
            ? "global"
            : "private";

        var definitionJson = JsonSerializer.Serialize(BuildDefinition());
        var existingId = (SavedProfilesCombo.SelectedItem as SearchProfile)?.Id;

        await _searchProfileRepository.UpsertAsync(new SearchProfileUpsert(
            Id: existingId,
            Name: name,
            Visibility: visibility,
            DefinitionJson: definitionJson,
            OwnerUsername: username));

        await LoadProfilesAsync();
        ProfileFeedbackText.Text = $"Profil '{name}' gespeichert.";
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var username = App.CurrentUser?.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            ProfileFeedbackText.Text = "Kein angemeldeter Benutzer.";
            return;
        }

        if (SavedProfilesCombo.SelectedItem is not SearchProfile profile)
        {
            ProfileFeedbackText.Text = "Bitte zuerst ein Profil auswählen.";
            return;
        }

        var deleted = await _searchProfileRepository.DeleteAsync(profile.Id, username, canDeleteAll: App.CurrentUser?.IsAdmin == true);
        if (!deleted)
        {
            ProfileFeedbackText.Text = "Profil konnte nicht gelöscht werden.";
            return;
        }

        await LoadProfilesAsync();
        ProfileFeedbackText.Text = $"Profil '{profile.Name}' gelöscht.";
    }

    private async void SearchSpotlight_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
            return;

        await ApplyAndSearchAsync(_vm);
    }

    private void PrimaryQueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var hasLineBreak = (PrimaryQueryTextBox.Text ?? string.Empty)
            .IndexOfAny(new[] { '\r', '\n' }) >= 0;

        PrimaryQueryTextBox.TextWrapping = hasLineBreak ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void SpotlightDetailFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (SpotlightDetailFrame.Content is ScriptItemView scriptItemView)
            scriptItemView.HideHistorySection = true;
    }
}

public sealed record SpotlightProfileDefinition(
    string? PrimaryQueryText,
    int ScopeFilterIndex,
    string? MainModule,
    string? RelatedModule,
    string? CustomerCode,
    string? Tags,
    string? ReferencedObjects,
    bool IncludeDeleted,
    bool SearchInHistory,
    Guid? FolderId,
    Guid? CollectionId);
