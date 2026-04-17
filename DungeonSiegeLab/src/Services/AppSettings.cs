namespace DungeonSiegeLab.Services;

/// <summary>
/// Runtime-accessible singleton that mirrors persisted settings.
/// SettingsViewModel writes here; BitsNodeViewModel reads here.
/// </summary>
public class AppSettings
{
    public static readonly AppSettings Instance = new();

    private AppSettings() { }

    public bool CollapseSubfoldersRecursively { get; set; } = false;
}
