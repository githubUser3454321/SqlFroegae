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
        EnableSecondGroupCheckBox.Checked += EnableSecondGroupCheckBox_Changed;
        EnableSecondGroupCheckBox.Unchecked += EnableSecondGroupCheckBox_Changed;
        EnableThirdGroupCheckBox.Checked += EnableThirdGroupCheckBox_Changed;
        EnableThirdGroupCheckBox.Unchecked += EnableThirdGroupCheckBox_Changed;
        UpdateGroupPanelVisibility();
    }

    public async Task InitializeFromAsync(LibrarySplitViewModel vm)
    {
        PrimaryQueryTextBox.Text = vm.QueryText;
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
        SecondFolderCombo.ItemsSource = vm.AvailableFolders;
        ThirdFolderCombo.ItemsSource = vm.AvailableFolders;

        CollectionCombo.ItemsSource = vm.AvailableCollections;
        CollectionCombo.SelectedItem = vm.SelectedCollection;
        SecondCollectionCombo.ItemsSource = vm.AvailableCollections;
        ThirdCollectionCombo.ItemsSource = vm.AvailableCollections;

        await LoadProfilesAsync();
    }

    public async Task ApplyAndSearchAsync(LibrarySplitViewModel vm)
    {
        ApplyTo(vm);

        if (EnableSecondGroupCheckBox.IsChecked == true || EnableThirdGroupCheckBox.IsChecked == true)
        {
            var combineWithAnd = GroupCombineModeCombo.SelectedIndex <= 0;
            await vm.SearchWithSpotlightGroupsAsync(BuildRuleGroups(), combineWithAnd);
            return;
        }

        await vm.SearchCommand.ExecuteAsync(null);
    }

    public void ApplyTo(LibrarySplitViewModel vm)
    {
        vm.IsAdvancedSearchExpanded = true;
        vm.QueryText = PrimaryQueryTextBox.Text;
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
        PrimaryQueryTextBox.Text = definition.PrimaryQueryText ?? string.Empty;
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

        GroupCombineModeCombo.SelectedIndex = definition.CombineWithAnd ? 0 : 1;
        EnableSecondGroupCheckBox.IsChecked = definition.SecondGroup is not null;
        EnableThirdGroupCheckBox.IsChecked = definition.ThirdGroup is not null;
        if (definition.SecondGroup is null)
        {
            ClearSecondGroup();
        }
        else
        {
            SecondQueryTextBox.Text = definition.SecondGroup.QueryText ?? string.Empty;
            SecondScopeCombo.SelectedIndex = definition.SecondGroup.ScopeFilterIndex;
            SecondMainModuleBox.Text = definition.SecondGroup.MainModule ?? string.Empty;
            SecondRelatedModuleBox.Text = definition.SecondGroup.RelatedModule ?? string.Empty;
            SecondCustomerCodeBox.Text = definition.SecondGroup.CustomerCode ?? string.Empty;
            SecondTagsBox.Text = definition.SecondGroup.Tags ?? string.Empty;
            SecondObjectsBox.Text = definition.SecondGroup.ReferencedObjects ?? string.Empty;
            SecondIncludeDeletedBox.IsChecked = definition.SecondGroup.IncludeDeleted;
            SecondSearchHistoryBox.IsChecked = definition.SecondGroup.SearchInHistory;
            SecondFolderCombo.SelectedItem = (SecondFolderCombo.ItemsSource as IEnumerable<FolderTreeOption>)?.FirstOrDefault(x => x.Id == definition.SecondGroup.FolderId);
            SecondCollectionCombo.SelectedItem = (SecondCollectionCombo.ItemsSource as IEnumerable<ScriptCollection>)?.FirstOrDefault(x => x.Id == definition.SecondGroup.CollectionId);
        }

        if (definition.ThirdGroup is null)
        {
            ClearThirdGroup();
            return;
        }

        ThirdQueryTextBox.Text = definition.ThirdGroup.QueryText ?? string.Empty;
        ThirdScopeCombo.SelectedIndex = definition.ThirdGroup.ScopeFilterIndex;
        ThirdMainModuleBox.Text = definition.ThirdGroup.MainModule ?? string.Empty;
        ThirdRelatedModuleBox.Text = definition.ThirdGroup.RelatedModule ?? string.Empty;
        ThirdCustomerCodeBox.Text = definition.ThirdGroup.CustomerCode ?? string.Empty;
        ThirdTagsBox.Text = definition.ThirdGroup.Tags ?? string.Empty;
        ThirdObjectsBox.Text = definition.ThirdGroup.ReferencedObjects ?? string.Empty;
        ThirdIncludeDeletedBox.IsChecked = definition.ThirdGroup.IncludeDeleted;
        ThirdSearchHistoryBox.IsChecked = definition.ThirdGroup.SearchInHistory;
        ThirdFolderCombo.SelectedItem = (ThirdFolderCombo.ItemsSource as IEnumerable<FolderTreeOption>)?.FirstOrDefault(x => x.Id == definition.ThirdGroup.FolderId);
        ThirdCollectionCombo.SelectedItem = (ThirdCollectionCombo.ItemsSource as IEnumerable<ScriptCollection>)?.FirstOrDefault(x => x.Id == definition.ThirdGroup.CollectionId);
    }

    private SpotlightProfileDefinition BuildDefinition()
    {
        SpotlightProfileGroupDefinition? secondGroup = null;
        SpotlightProfileGroupDefinition? thirdGroup = null;
        if (EnableSecondGroupCheckBox.IsChecked == true)
        {
            secondGroup = new SpotlightProfileGroupDefinition(
                QueryText: SecondQueryTextBox.Text,
                ScopeFilterIndex: SecondScopeCombo.SelectedIndex < 0 ? 0 : SecondScopeCombo.SelectedIndex,
                MainModule: SecondMainModuleBox.Text,
                RelatedModule: SecondRelatedModuleBox.Text,
                CustomerCode: SecondCustomerCodeBox.Text,
                Tags: SecondTagsBox.Text,
                ReferencedObjects: SecondObjectsBox.Text,
                IncludeDeleted: SecondIncludeDeletedBox.IsChecked == true,
                SearchInHistory: SecondSearchHistoryBox.IsChecked == true,
                FolderId: (SecondFolderCombo.SelectedItem as FolderTreeOption)?.Id,
                CollectionId: (SecondCollectionCombo.SelectedItem as ScriptCollection)?.Id);
        }

        if (EnableThirdGroupCheckBox.IsChecked == true)
        {
            thirdGroup = new SpotlightProfileGroupDefinition(
                QueryText: ThirdQueryTextBox.Text,
                ScopeFilterIndex: ThirdScopeCombo.SelectedIndex < 0 ? 0 : ThirdScopeCombo.SelectedIndex,
                MainModule: ThirdMainModuleBox.Text,
                RelatedModule: ThirdRelatedModuleBox.Text,
                CustomerCode: ThirdCustomerCodeBox.Text,
                Tags: ThirdTagsBox.Text,
                ReferencedObjects: ThirdObjectsBox.Text,
                IncludeDeleted: ThirdIncludeDeletedBox.IsChecked == true,
                SearchInHistory: ThirdSearchHistoryBox.IsChecked == true,
                FolderId: (ThirdFolderCombo.SelectedItem as FolderTreeOption)?.Id,
                CollectionId: (ThirdCollectionCombo.SelectedItem as ScriptCollection)?.Id);
        }

        return new SpotlightProfileDefinition(
            PrimaryQueryText: PrimaryQueryTextBox.Text,
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
            CollectionId: (CollectionCombo.SelectedItem as ScriptCollection)?.Id,
            CombineWithAnd: GroupCombineModeCombo.SelectedIndex <= 0,
            SecondGroup: secondGroup,
            ThirdGroup: thirdGroup);
    }

    private IReadOnlyList<SpotlightFilterGroup> BuildRuleGroups()
    {
        var groups = new List<SpotlightFilterGroup>
        {
            new(
                QueryText: PrimaryQueryTextBox.Text,
                ScopeFilterIndex: ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex,
                MainModuleFilterText: MainModuleBox.Text,
                RelatedModuleFilterText: RelatedModuleBox.Text,
                CustomerCodeFilterText: CustomerCodeBox.Text,
                TagsFilterText: TagsBox.Text,
                ObjectFilterText: ObjectsBox.Text,
                IncludeDeleted: IncludeDeletedBox.IsChecked == true,
                SearchInHistory: SearchHistoryBox.IsChecked == true,
                FolderId: (FolderCombo.SelectedItem as FolderTreeOption)?.Id,
                CollectionId: (CollectionCombo.SelectedItem as ScriptCollection)?.Id)
        };

        if (EnableSecondGroupCheckBox.IsChecked == true)
        {
            groups.Add(new SpotlightFilterGroup(
                QueryText: SecondQueryTextBox.Text,
                ScopeFilterIndex: SecondScopeCombo.SelectedIndex < 0 ? 0 : SecondScopeCombo.SelectedIndex,
                MainModuleFilterText: SecondMainModuleBox.Text,
                RelatedModuleFilterText: SecondRelatedModuleBox.Text,
                CustomerCodeFilterText: SecondCustomerCodeBox.Text,
                TagsFilterText: SecondTagsBox.Text,
                ObjectFilterText: SecondObjectsBox.Text,
                IncludeDeleted: SecondIncludeDeletedBox.IsChecked == true,
                SearchInHistory: SecondSearchHistoryBox.IsChecked == true,
                FolderId: (SecondFolderCombo.SelectedItem as FolderTreeOption)?.Id,
                CollectionId: (SecondCollectionCombo.SelectedItem as ScriptCollection)?.Id));
        }

        if (EnableThirdGroupCheckBox.IsChecked == true)
        {
            groups.Add(new SpotlightFilterGroup(
                QueryText: ThirdQueryTextBox.Text,
                ScopeFilterIndex: ThirdScopeCombo.SelectedIndex < 0 ? 0 : ThirdScopeCombo.SelectedIndex,
                MainModuleFilterText: ThirdMainModuleBox.Text,
                RelatedModuleFilterText: ThirdRelatedModuleBox.Text,
                CustomerCodeFilterText: ThirdCustomerCodeBox.Text,
                TagsFilterText: ThirdTagsBox.Text,
                ObjectFilterText: ThirdObjectsBox.Text,
                IncludeDeleted: ThirdIncludeDeletedBox.IsChecked == true,
                SearchInHistory: ThirdSearchHistoryBox.IsChecked == true,
                FolderId: (ThirdFolderCombo.SelectedItem as FolderTreeOption)?.Id,
                CollectionId: (ThirdCollectionCombo.SelectedItem as ScriptCollection)?.Id));
        }

        return groups;
    }

    private void EnableSecondGroupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateGroupPanelVisibility();
        if (EnableSecondGroupCheckBox.IsChecked != true)
        {
            ClearSecondGroup();
            EnableThirdGroupCheckBox.IsChecked = false;
        }
    }

    private void EnableThirdGroupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (EnableThirdGroupCheckBox.IsChecked == true)
            EnableSecondGroupCheckBox.IsChecked = true;

        UpdateGroupPanelVisibility();
        if (EnableThirdGroupCheckBox.IsChecked != true)
            ClearThirdGroup();
    }

    private void UpdateGroupPanelVisibility()
    {
        SecondGroupPanel.Visibility = EnableSecondGroupCheckBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

        ThirdGroupPanel.Visibility = EnableThirdGroupCheckBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClearSecondGroup()
    {
        SecondQueryTextBox.Text = string.Empty;
        SecondScopeCombo.SelectedIndex = 0;
        SecondMainModuleBox.Text = string.Empty;
        SecondRelatedModuleBox.Text = string.Empty;
        SecondCustomerCodeBox.Text = string.Empty;
        SecondTagsBox.Text = string.Empty;
        SecondObjectsBox.Text = string.Empty;
        SecondIncludeDeletedBox.IsChecked = false;
        SecondSearchHistoryBox.IsChecked = false;
        SecondFolderCombo.SelectedItem = null;
        SecondCollectionCombo.SelectedItem = null;
    }

    private void ClearThirdGroup()
    {
        ThirdQueryTextBox.Text = string.Empty;
        ThirdScopeCombo.SelectedIndex = 0;
        ThirdMainModuleBox.Text = string.Empty;
        ThirdRelatedModuleBox.Text = string.Empty;
        ThirdCustomerCodeBox.Text = string.Empty;
        ThirdTagsBox.Text = string.Empty;
        ThirdObjectsBox.Text = string.Empty;
        ThirdIncludeDeletedBox.IsChecked = false;
        ThirdSearchHistoryBox.IsChecked = false;
        ThirdFolderCombo.SelectedItem = null;
        ThirdCollectionCombo.SelectedItem = null;
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
    string? PrimaryQueryText,
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
    Guid? CollectionId,
    bool CombineWithAnd,
    SpotlightProfileGroupDefinition? SecondGroup,
    SpotlightProfileGroupDefinition? ThirdGroup);

public sealed record SpotlightProfileGroupDefinition(
    string? QueryText,
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
