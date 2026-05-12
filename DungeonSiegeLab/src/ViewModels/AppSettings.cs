namespace DungeonSiegeLab.ViewModels;

public class AppSettings
{
    public static readonly AppSettings Instance = new();

    private AppSettings() { }

    public ICollapseState CollapseState { get; private set; } = DefaultCollapseState.Instance;

    public bool CollapseSubfoldersRecursively
    {
        get => CollapseState is RecursiveCollapseState;
        set => CollapseState = value ? RecursiveCollapseState.Instance : DefaultCollapseState.Instance;
    }
}
