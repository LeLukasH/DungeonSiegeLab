using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace DungeonSiegeLab.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ProjectBrowserViewModel ProjectBrowser { get; } = new();
    public TextureLabViewModel TextureLab { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    [ObservableProperty] private int _selectedTabIndex = 0; // 0 = Browser, 1 = Lab, 2 = Settings

    public bool IsProjectBrowserActive => SelectedTabIndex == 0;
    public bool IsTextureLabActive     => SelectedTabIndex == 1;
    public bool IsSettingsActive       => SelectedTabIndex == 2;

    private FileWatcherService? _fileWatcher;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsProjectBrowserActive));
        OnPropertyChanged(nameof(IsTextureLabActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    public MainViewModel()
    {
        Console.WriteLine("MainViewModel initialized and waiting for Bits folder load.");
        ProjectBrowser.TexturesIdentified += OnTexturesIdentified;
        ProjectBrowser.BitsFolderLoaded += OnBitsFolderLoaded;
        TextureLab.BackRequested += () => SelectedTabIndex = 0;
    }

    private async void OnTexturesIdentified(List<TextureReference> textures)
    {
        SelectedTabIndex = 1;
        await TextureLab.LoadTexturesAsync(textures);
    }

    private void OnBitsFolderLoaded(string path)
    {
        Console.WriteLine($"Bits folder loaded, starting watcher for: {path}");
        _fileWatcher?.Dispose();
        _fileWatcher = new FileWatcherService(path);
        _fileWatcher.FileChanged += OnWatchedFileChanged;
        try
        {
            _fileWatcher.StartWatching();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start file watcher: {ex.Message}");
        }
    }

    private void OnWatchedFileChanged(string filePath)
    {
        Console.WriteLine($"MainViewModel received change notification for watched file: {filePath}");
        Dispatcher.UIThread.Post(async () =>
        {
            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (mainWindow == null)
            {
                Console.WriteLine("MainWindow is null when trying to show file change popup.");
                return;
            }

            var message = $"File '{filePath}' has been changed.";
            var okButton = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock { Text = message, Margin = new Avalonia.Thickness(20) });
            stackPanel.Children.Add(okButton);

            var dialog = new Window
            {
                Title = "File Change Notification",
                Content = stackPanel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true,
                CanResize = false
            };

            okButton.Click += (_, _) => dialog.Close();
            await dialog.ShowDialog(mainWindow);
        });
    }

    [RelayCommand] private void SwitchToLab()      => SelectedTabIndex = 1;
    [RelayCommand] private void SwitchToBrowser()  => SelectedTabIndex = 0;
    [RelayCommand] private void SwitchToSettings() => SelectedTabIndex = 2;
}
