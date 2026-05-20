using System.Diagnostics;

namespace DungeonSiegeLab.Services;

public class ExternalTextureToolService
{
    public async Task<string> ConvertRawToPsdAsync(string toolPath, string rawPath)
    {
        EnsureToolExists(toolPath, "RawToPsd.exe");

        var expectedPsdPath = Path.ChangeExtension(rawPath, ".psd");
        SafeDelete(expectedPsdPath);

        await RunToolAsync(toolPath, $"\"{rawPath}\"", "RAW to PSD");

        if (!File.Exists(expectedPsdPath))
            throw new FileNotFoundException($"RAW to PSD conversion did not produce: {expectedPsdPath}");

        return expectedPsdPath;
    }

    public async Task<string> ConvertPsdToRawAsync(string toolPath, string psdPath)
    {
        EnsureToolExists(toolPath, "PsdToRaw.exe");

        var expectedRawPath = Path.ChangeExtension(psdPath, ".raw");
        SafeDelete(expectedRawPath);

        await RunToolAsync(toolPath, $"\"{psdPath}\"", "PSD to RAW");

        if (!File.Exists(expectedRawPath))
            throw new FileNotFoundException($"PSD to RAW conversion did not produce: {expectedRawPath}");

        return expectedRawPath;
    }

    private static void EnsureToolExists(string toolPath, string toolName)
    {
        if (!File.Exists(toolPath))
            throw new FileNotFoundException(
                $"{toolName} was not found. Expected bundled location: {toolPath}");
    }

    private static async Task RunToolAsync(string fileName, string arguments, string operationName)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(fileName) ?? AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(error))
                error = await process.StandardOutput.ReadToEndAsync();

            throw new InvalidOperationException(
                $"{operationName} conversion failed (exit {process.ExitCode}). {error}".Trim());
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors for previously generated files.
        }
    }
}
