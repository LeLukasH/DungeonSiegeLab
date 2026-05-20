using DungeonSiegeLab.Services;

namespace DungeonSiegeLab.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
        TreeStateService.Instance.Load();
        AppSettings.Instance.CollapseSubfoldersRecursively = TreeStateService.Instance.CollapseSubfoldersRecursively;
    }

    public bool CollapseSubfoldersRecursively
    {
        get => TreeStateService.Instance.CollapseSubfoldersRecursively;
        set
        {
            if (TreeStateService.Instance.CollapseSubfoldersRecursively == value)
                return;

            TreeStateService.Instance.CollapseSubfoldersRecursively = value;
            AppSettings.Instance.CollapseSubfoldersRecursively = value;
            OnPropertyChanged();
        }
    }
}
