using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSiegeLab.Services;

namespace DungeonSiegeLab.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _collapseSubfoldersRecursively;

    public SettingsViewModel()
    {
        // TreeStateService.Load() is called by ProjectBrowserViewModel first,
        // so the state is already loaded when this runs.
        _collapseSubfoldersRecursively = TreeStateService.Instance.CollapseSubfoldersRecursively;
        AppSettings.Instance.CollapseSubfoldersRecursively = _collapseSubfoldersRecursively;
    }

    partial void OnCollapseSubfoldersRecursivelyChanged(bool value)
    {
        AppSettings.Instance.CollapseSubfoldersRecursively = value;
        TreeStateService.Instance.CollapseSubfoldersRecursively = value;
    }
}
