using Avalonia.Media;

namespace DungeonSiegeLab.ViewModels;

public class SnippetSpan
{
    private static readonly IBrush HighlightBg  = new SolidColorBrush(Color.Parse("#3d2f5c"));
    private static readonly IBrush MatchFg      = new SolidColorBrush(Color.Parse("#f7dba6"));
    private static readonly IBrush NormalFg     = new SolidColorBrush(Color.Parse("#89b4fa"));

    private static readonly IBrush NameFg = new SolidColorBrush(Color.Parse("#cba6f7"));

    public string Text { get; }
    public bool   IsMatch { get; }
    public IBrush Background     => IsMatch ? HighlightBg : Brushes.Transparent;
    public IBrush Foreground     => IsMatch ? MatchFg : NormalFg;
    public IBrush NameForeground => IsMatch ? MatchFg : NameFg;

    public SnippetSpan(string text, bool isMatch) { Text = text; IsMatch = isMatch; }

    public static IReadOnlyList<SnippetSpan> Split(string line, string query)
    {
        var spans = new List<SnippetSpan>();
        int pos = 0;
        while (pos < line.Length)
        {
            int idx = line.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) { spans.Add(new SnippetSpan(line[pos..], false)); break; }
            if (idx > pos) spans.Add(new SnippetSpan(line[pos..idx], false));
            spans.Add(new SnippetSpan(line[idx..(idx + query.Length)], true));
            pos = idx + query.Length;
        }
        if (spans.Count == 0) spans.Add(new SnippetSpan(line, false));
        return spans;
    }
}

public class SearchResultViewModel
{
    public BitsComponentViewModel Node { get; }
    public string Name => Node.Name;
    public string RelativePath { get; }
    public string Query { get; }
    public IReadOnlyList<string> MatchSnippets { get; }
    public IReadOnlyList<IReadOnlyList<SnippetSpan>> HighlightedSnippets { get; }
    public IReadOnlyList<SnippetSpan> HighlightedName { get; }
    public bool HasSnippet => MatchSnippets.Count > 0;
    public bool IsUntankSource { get; init; }
    public bool IsFirstUntankResult { get; init; }

    public SearchResultViewModel(BitsComponentViewModel node, string relativePath, IReadOnlyList<string> matchSnippets, string query)
    {
        Node = node;
        RelativePath = relativePath;
        MatchSnippets = matchSnippets;
        Query = query;
        HighlightedSnippets = matchSnippets.Select(s => SnippetSpan.Split(s, query)).ToList();
        HighlightedName = SnippetSpan.Split(Node.Name, query);
    }
}
