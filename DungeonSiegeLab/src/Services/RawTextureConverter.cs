using DungeonSiegeLab.Models;
using ImageMagick;

namespace DungeonSiegeLab.Services;

public class RawTextureConverter
{
    private readonly ExternalTextureToolService _toolService = new();
    private readonly TextureExportStrategyFactory _exportStrategyFactory;
    private readonly TextureExportDependencies _exportDependencies;

    public RawTextureConverter()
    {
        _exportDependencies = new TextureExportDependencies
        {
            EnsureWorkingPsdAsync = EnsureWorkingPsdAsync,
            ExportRawAsync = ExportRawAsync
        };

        _exportStrategyFactory = new TextureExportStrategyFactory(
        [
            PngTextureExportStrategy.Instance,
            PsdTextureExportStrategy.Instance,
            RawTextureExportStrategy.Instance
        ]);
    }

    public async Task<LoadedTexture> LoadTextureAsync(TextureReference textureRef)
    {
        if (textureRef.ResolvedPath is null)
            throw new FileNotFoundException($"Texture '{textureRef.TextureName}' was not found in /Bits.");

        return await LoadFromPathAsync(textureRef.TextureName, textureRef.ResolvedPath);
    }

    public async Task<LoadedTexture> LoadFromPathAsync(string name, string filePath)
    {
        var format = TextureFormatExtensions.FromPath(filePath);
        var previewPngPath = await CreatePreviewPngAsync(filePath, format);
        var (width, height) = await GetDimensionsFromPngAsync(previewPngPath);

        return new LoadedTexture
        {
            Name = name,
            OriginalPath = filePath,
            OriginalFormat = format,
            PngCachePath = previewPngPath,
            WorkingPsdPath = format == TextureFormat.Psd ? filePath : null,
            Width = width,
            Height = height
        };
    }

    public async Task SaveAsAsync(LoadedTexture texture, string targetPath)
    {
        var targetFormat = TextureFormatExtensions.FromPath(targetPath);
        var normalizedTargetPath = Path.ChangeExtension(targetPath, targetFormat.ToExtension());
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedTargetPath)!);
        var strategy = _exportStrategyFactory.GetStrategy(targetFormat);
        await strategy.ExportAsync(texture, normalizedTargetPath, _exportDependencies);
    }

    public async Task SaveToProjectAsync(LoadedTexture texture, string bitsRootPath, string relativePath, string textureName)
    {
        var targetDir = Path.Combine(bitsRootPath, relativePath);
        Directory.CreateDirectory(targetDir);

        var rawPath = Path.Combine(targetDir, textureName + ".raw");
        var gasPath = Path.Combine(targetDir, textureName + ".gas");

        await SaveAsAsync(texture, rawPath);

        var gasContent = GenerateTextureGas(textureName, relativePath);
        await File.WriteAllTextAsync(gasPath, gasContent);
    }

    public async Task<LoadedTexture> ImportReplacementAsync(string sourcePath, string originalName)
    {
        return await LoadFromPathAsync(originalName, sourcePath);
    }

    private async Task<string> CreatePreviewPngAsync(string filePath, TextureFormat format)
    {
        return format switch
        {
            TextureFormat.Png => await CopyToTempAsync(filePath, ".png"),
            TextureFormat.Psd => await ConvertPsdToPngAsync(filePath),
            TextureFormat.Raw => await CreatePreviewFromRawAsync(filePath),
            _ => throw new InvalidOperationException($"Unsupported texture format: {format}")
        };
    }

    private async Task<string> CreatePreviewFromRawAsync(string rawPath)
    {
        var tempDir = CreateTempDirectory();
        var tempRawPath = Path.Combine(tempDir, Path.GetFileName(rawPath));
        await Task.Run(() => File.Copy(rawPath, tempRawPath, overwrite: true));

        try
        {
            var psdPath = await _toolService.ConvertRawToPsdAsync(BundledToolPaths.RawToPsdPath, tempRawPath);
            return await ConvertPsdToPngAsync(psdPath);
        }
        finally
        {
            SafeDelete(tempRawPath);
            SafeDelete(Path.ChangeExtension(tempRawPath, ".psd"));
            SafeDeleteDirectory(tempDir);
        }
    }

    private async Task<string> EnsureWorkingPsdAsync(LoadedTexture texture)
    {
        if (!string.IsNullOrWhiteSpace(texture.WorkingPsdPath) && File.Exists(texture.WorkingPsdPath))
            return texture.WorkingPsdPath;

        var tempPsdPath = CreateTempPath(".psd");

        switch (texture.OriginalFormat)
        {
            case TextureFormat.Raw:
                var tempDir = CreateTempDirectory();
                var tempRawPath = Path.Combine(tempDir, Path.GetFileName(texture.OriginalPath));
                await Task.Run(() => File.Copy(texture.OriginalPath, tempRawPath, overwrite: true));
                var generatedPsdPath = await _toolService.ConvertRawToPsdAsync(BundledToolPaths.RawToPsdPath, tempRawPath);
                await Task.Run(() => File.Copy(generatedPsdPath, tempPsdPath, overwrite: true));
                SafeDelete(tempRawPath);
                SafeDelete(generatedPsdPath);
                SafeDeleteDirectory(tempDir);
                break;

            case TextureFormat.Png:
                await ConvertPngToPsdAsync(texture.OriginalPath, tempPsdPath);
                break;

            case TextureFormat.Psd:
                await Task.Run(() => File.Copy(texture.OriginalPath, tempPsdPath, overwrite: true));
                break;

            default:
                throw new InvalidOperationException($"Unsupported texture format: {texture.OriginalFormat}");
        }

        texture.WorkingPsdPath = tempPsdPath;
        return tempPsdPath;
    }

    private async Task ExportRawAsync(string sourcePsdPath, string targetRawPath)
    {
        var tempDir = CreateTempDirectory();
        var tempPsdPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(targetRawPath) + ".psd");

        try
        {
            await Task.Run(() => File.Copy(sourcePsdPath, tempPsdPath, overwrite: true));
            var generatedRawPath = await _toolService.ConvertPsdToRawAsync(BundledToolPaths.PsdToRawPath, tempPsdPath);
            await Task.Run(() => File.Copy(generatedRawPath, targetRawPath, overwrite: true));
        }
        finally
        {
            SafeDelete(tempPsdPath);
            SafeDelete(Path.ChangeExtension(tempPsdPath, ".raw"));
            SafeDeleteDirectory(tempDir);
        }
    }

    private static async Task<string> ConvertPsdToPngAsync(string psdPath)
    {
        var pngPath = CreateTempPath(".png");

        await Task.Run(() =>
        {
            using var image = new MagickImage(psdPath);
            image.Write(pngPath, MagickFormat.Png);
        });

        return pngPath;
    }

    private static async Task ConvertPngToPsdAsync(string pngPath, string psdPath)
    {
        await Task.Run(() =>
        {
            using var image = new MagickImage(pngPath);
            image.Write(psdPath, MagickFormat.Psd);
        });
    }

    private static async Task<(int Width, int Height)> GetDimensionsFromPngAsync(string pngPath)
    {
        return await Task.Run(() =>
        {
            using var image = new MagickImage(pngPath);
            return ((int)image.Width, (int)image.Height);
        });
    }

    private static async Task<string> CopyToTempAsync(string sourcePath, string extension)
    {
        var tempPath = CreateTempPath(extension);
        await Task.Run(() => File.Copy(sourcePath, tempPath, overwrite: true));
        return tempPath;
    }

    private static string CreateTempPath(string extension) =>
        Path.ChangeExtension(Path.GetTempFileName(), extension);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "DungeonSiegeLab", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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
            // Ignore temp file cleanup errors.
        }
    }

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Ignore temp directory cleanup errors.
        }
    }

    private static string GenerateTextureGas(string name, string path) =>
        $$"""
        // Auto-generated by DungeonSiegeLab
        [t:template,n:{{name}}]
        {
            [aspect]
            {
                [textures]
                {
                    0 = {{name}};
                }
            }
        }
        """;
}
