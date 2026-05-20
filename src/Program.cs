using Avalonia;
using DungeonSiegeLab.Views;
using System;
using System.IO;

namespace DungeonSiegeLab;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Redirect console output to a log file
        var logFile = Path.Combine(AppContext.BaseDirectory, "DungeonSiegeLab.log");
        var logWriter = new StreamWriter(logFile, append: true) { AutoFlush = true };
        Console.SetOut(logWriter);
        Console.SetError(logWriter);

        // Ensure the writer is closed on exit
        AppDomain.CurrentDomain.ProcessExit += (s, e) => logWriter?.Dispose();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
