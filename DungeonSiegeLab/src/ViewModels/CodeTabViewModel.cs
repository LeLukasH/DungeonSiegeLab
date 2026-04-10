using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.ViewModels;

/// <summary>
/// Represents one open template in the code viewer.
/// IsPreview = true → italic tab title, replaced on next single-click (VSCode preview behavior).
/// IsPreview = false → permanent tab, stays until explicitly closed.
/// </summary>
public partial class CodeTabViewModel : ViewModelBase
{
    public BitsNodeViewModel Node { get; }

    [ObservableProperty]
    private bool _isPreview;

    public string Name => Node.Name;
    public string SourceCode => Node.Node is BitsTemplate t ? t.SourceCode : $"// {Node.Name}";

    /// <summary>Italic when preview, normal when permanent — bound directly in XAML.</summary>
    public FontStyle TabFontStyle => IsPreview ? FontStyle.Italic : FontStyle.Normal;

    partial void OnIsPreviewChanged(bool value) => OnPropertyChanged(nameof(TabFontStyle));

    public CodeTabViewModel(BitsNodeViewModel node, bool isPreview)
    {
        Node = node;
        _isPreview = isPreview;
    }
}
