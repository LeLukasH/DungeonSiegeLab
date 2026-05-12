namespace DungeonSiegeLab.ViewModels;

public interface ICollapseState
{
    void Collapse(BitsComponentViewModel vm);
}

public class DefaultCollapseState : ICollapseState
{
    public static readonly DefaultCollapseState Instance = new();
    public void Collapse(BitsComponentViewModel vm) { }
}

public class RecursiveCollapseState : ICollapseState
{
    public static readonly RecursiveCollapseState Instance = new();
    public void Collapse(BitsComponentViewModel vm) => vm.CollapseAll();
}
