namespace DungeonSiegeLab.Services;

public static class BundledToolPaths
{
    public static string ToolsRoot =>
        Path.Combine(AppContext.BaseDirectory, "Tools");

    public static string RawToPsdPath =>
        Path.Combine(ToolsRoot, "RawToPsd", "RawToPsd.exe");

    public static string PsdToRawPath =>
        Path.Combine(ToolsRoot, "PsdToRaw", "PsdToRaw.exe");
}
