using Microsoft.UI.Xaml.Controls;
using SqlFroega.ViewModels;

namespace SqlFroega.Views;

public sealed partial class SpotlightQueryStudioView : UserControl
{
    public SpotlightQueryStudioView()
    {
        InitializeComponent();
    }

    public void InitializeFrom(LibrarySplitViewModel vm)
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
        vm.SelectedCollection = CollectionCombo.SelectedItem as SqlFroega.Application.Models.ScriptCollection;
    }
}
