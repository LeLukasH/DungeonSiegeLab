namespace DungeonSiegeLab.ViewModels;

public class SearchResultViewModel
{
    public BitsNodeViewModel Node { get; }
    public string Name => Node.Name;
    public string RelativePath { get; }
    public string MatchSnippet { get; }
    public bool HasSnippet => !string.IsNullOrEmpty(MatchSnippet);

    public SearchResultViewModel(BitsNodeViewModel node, string relativePath, string matchSnippet)
    {
        Node = node;
        RelativePath = relativePath;
        MatchSnippet = matchSnippet;
    }
}
