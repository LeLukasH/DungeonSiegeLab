using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.ViewModels;

/// <summary>
/// Represents one open template in the code viewer.
/// IsPreview = true → italic tab title, replaced on next single-click (VSCode preview behavior).
/// IsPreview = false → permanent tab, stays until explicitly closed.
/// </summary>
public partial class CodeTabViewModel : ViewModelBase
{
    public BitsComponentViewModel Node { get; }

    [ObservableProperty] private bool _isPreview;
    // Adam integration point: bind list/dots/cards to this collection.
    [ObservableProperty] private ObservableCollection<DependencyReference> _dependencies = new();
    // Selected dependency for details popup or side inspector.
    [ObservableProperty] private DependencyReference? _selectedDependency;
    // Toggle for dependency details popup.
    [ObservableProperty] private bool _isDependencyPopupOpen;

    [ObservableProperty] private bool _isStatusExpanded;

    public string Name => Node.Name;
    public string SourceCode => Node.Node is BitsTemplate t ? t.SourceCode : $"// {Node.Name}";
    // Frontend summary helpers (chips/counters in tab overlay).
    public bool HasDependencies => Dependencies.Count > 0;
    public int LocalDependencyCount => Dependencies.Count(d => !d.IsInherited);
    public int InheritedDependencyCount => Dependencies.Count(d => d.IsInherited);
    public string DependencySummary =>
        $"Local:{LocalDependencyCount} | Inherited:{InheritedDependencyCount} | Total:{Dependencies.Count}";

    /// <summary>Italic when preview, normal when permanent — bound directly in XAML.</summary>
    public FontStyle TabFontStyle => IsPreview ? FontStyle.Italic : FontStyle.Normal;

    partial void OnIsPreviewChanged(bool value) => OnPropertyChanged(nameof(TabFontStyle));

    public CodeTabViewModel(BitsComponentViewModel node, bool isPreview)
    {
        Node = node;
        _isPreview = isPreview;

        node.LoadInto(this);
    }

    internal async Task LoadRawContentAsync()
    {
        try
        {
            var info = new FileInfo(Node.FullPath);
            if (info.Length > 5 * 1024 * 1024)
            {
                SourceCode = $"// File too large to display ({info.Length / 1024:N0} KB)";
                return;
            }

            var bytes = await File.ReadAllBytesAsync(Node.FullPath);

            // Binary detection: null byte in first 8 KB
            int check = Math.Min(bytes.Length, 8192);
            for (int i = 0; i < check; i++)
            {
                if (bytes[i] == 0)
                {
                    SourceCode = "// Binary file — cannot display content";
                    return;
                }
            }

            SourceCode = System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            SourceCode = $"// Could not read file: {ex.Message}";
        }
    }

    public void SetDependencies(IEnumerable<DependencyReference> dependencies)
    {
        // Replace entire collection per Identify run to keep UI state consistent with parser output.
        Dependencies = new ObservableCollection<DependencyReference>(dependencies);
        OnPropertyChanged(nameof(HasDependencies));
        OnPropertyChanged(nameof(LocalDependencyCount));
        OnPropertyChanged(nameof(InheritedDependencyCount));
        OnPropertyChanged(nameof(DependencySummary));
    }

    [RelayCommand]
    private void ToggleStatus()
    {
        Console.WriteLine("Toggle clicked");
        IsStatusExpanded = !IsStatusExpanded;
    }

    [RelayCommand]
    private void OpenDependency(DependencyReference dep)
    {
        SelectedDependency = dep;
        IsDependencyPopupOpen = true;
    }

    [RelayCommand]
    private void CloseDependency()
    {
        IsDependencyPopupOpen = false;
        SelectedDependency = null;
    }
}
