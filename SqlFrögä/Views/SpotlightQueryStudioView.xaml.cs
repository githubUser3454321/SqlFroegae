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
    private readonly ISearchProfileRepository _searchProfileRepository;
    private readonly List<SearchProfile> _loadedProfiles = new();

    public SpotlightQueryStudioView()
    {
        InitializeComponent();
        _searchProfileRepository = App.Services.GetRequiredService<ISearchProfileRepository>();
    }

    public async Task InitializeFromAsync(LibrarySplitViewModel vm)
    {
        ScopeCombo.SelectedIndex = vm.ScopeFilterIndex;
        MainModuleBox.Text = vm.MainModuleFilterText;
        RelatedModuleBox.Text = vm.RelatedModuleFilterText;
        CustomerCodeBox.Text = vm.CustomerCodeFilterText;
        TagsBox.Text = vm.TagsFilterText;
        ObjectsBox.Text = vm.ObjectFilterText;
        IncludeDeletedBox.IsChecked = vm.IncludeDeleted;
        SearchHistoryBox.IsChecked = vm.SearchInHistory;

        DisplayFolderStructureToggle.IsOn = vm.DisplayFolderStructure;

        FolderCombo.ItemsSource = vm.AvailableFolders;
        FolderCombo.SelectedItem = vm.SelectedFolder;

        CollectionCombo.ItemsSource = vm.AvailableCollections;
        CollectionCombo.SelectedItem = vm.SelectedCollection;

        await LoadProfilesAsync();
    }

    public void ApplyTo(LibrarySplitViewModel vm)
    {
        vm.IsAdvancedSearchExpanded = true;
        vm.ScopeFilterIndex = ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex;
        vm.MainModuleFilterText = MainModuleBox.Text;
        vm.RelatedModuleFilterText = RelatedModuleBox.Text;
        vm.CustomerCodeFilterText = CustomerCodeBox.Text;
        vm.TagsFilterText = TagsBox.Text;
        vm.ObjectFilterText = ObjectsBox.Text;
        vm.IncludeDeleted = IncludeDeletedBox.IsChecked == true;
        vm.SearchInHistory = SearchHistoryBox.IsChecked == true;
        vm.DisplayFolderStructure = DisplayFolderStructureToggle.IsOn;

        vm.SelectedFolder = FolderCombo.SelectedItem as FolderTreeOption;
        vm.SelectedCollection = CollectionCombo.SelectedItem as ScriptCollection;
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

    private void ApplyDefinition(SpotlightProfileDefinition definition)
    {
        ScopeCombo.SelectedIndex = definition.ScopeFilterIndex;
        MainModuleBox.Text = definition.MainModule ?? string.Empty;
        RelatedModuleBox.Text = definition.RelatedModule ?? string.Empty;
        CustomerCodeBox.Text = definition.CustomerCode ?? string.Empty;
        TagsBox.Text = definition.Tags ?? string.Empty;
        ObjectsBox.Text = definition.ReferencedObjects ?? string.Empty;
        IncludeDeletedBox.IsChecked = definition.IncludeDeleted;
        SearchHistoryBox.IsChecked = definition.SearchInHistory;
        DisplayFolderStructureToggle.IsOn = definition.DisplayFolderStructure;

        FolderCombo.SelectedItem = (FolderCombo.ItemsSource as IEnumerable<FolderTreeOption>)?.FirstOrDefault(x => x.Id == definition.FolderId);
        CollectionCombo.SelectedItem = (CollectionCombo.ItemsSource as IEnumerable<ScriptCollection>)?.FirstOrDefault(x => x.Id == definition.CollectionId);
    }

    private SpotlightProfileDefinition BuildDefinition()
    {
        return new SpotlightProfileDefinition(
            ScopeFilterIndex: ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex,
            MainModule: MainModuleBox.Text,
            RelatedModule: RelatedModuleBox.Text,
            CustomerCode: CustomerCodeBox.Text,
            Tags: TagsBox.Text,
            ReferencedObjects: ObjectsBox.Text,
            IncludeDeleted: IncludeDeletedBox.IsChecked == true,
            SearchInHistory: SearchHistoryBox.IsChecked == true,
            DisplayFolderStructure: DisplayFolderStructureToggle.IsOn,
            FolderId: (FolderCombo.SelectedItem as FolderTreeOption)?.Id,
            CollectionId: (CollectionCombo.SelectedItem as ScriptCollection)?.Id);
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
}

public sealed record SpotlightProfileDefinition(
    int ScopeFilterIndex,
    string? MainModule,
    string? RelatedModule,
    string? CustomerCode,
    string? Tags,
    string? ReferencedObjects,
    bool IncludeDeleted,
    bool SearchInHistory,
    bool DisplayFolderStructure,
    Guid? FolderId,
    Guid? CollectionId);
