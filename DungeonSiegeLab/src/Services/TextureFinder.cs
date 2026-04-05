using System.Text.RegularExpressions;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

/// <summary>
/// Nájde všetky textúry referencované v zdrojovom kóde template.
/// Podľa špecifikácie zo zadania:
///   - aspect->textures->0,1,2,...
///   - aspect->model (implicit texture)
///   - gui->inventory_icon
///   - akýkoľvek [component] atribút obsahujúci ".*texture.*"
/// </summary>
public class TextureFinder
{
    // aspect->textures->0, textures->1, ...
    private static readonly Regex AspectTexturesRegex = new(
        @"textures\s*\{[^}]*\d+\s*=\s*(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    // model = meno_modelu  →  implicitná textúra má rovnaký prefix
    private static readonly Regex AspectModelRegex = new(
        @"\bmodel\s*=\s*(\w+)\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // gui->inventory_icon = meno
    private static readonly Regex InventoryIconRegex = new(
        @"\binventory_icon\s*=\s*(\w+)\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Akýkoľvek atribút obsahujúci "texture" v názve
    private static readonly Regex GenericTextureAttrRegex = new(
        @"\b\w*texture\w*\s*=\s*(\w+)\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public List<TextureReference> FindInTemplate(BitsTemplate template)
        => FindInCode(template.SourceCode);

    public List<TextureReference> FindInCode(string sourceCode)
    {
        var results = new List<TextureReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfNew(TextureReference tref)
        {
            if (seen.Add(tref.TextureName))
                results.Add(tref);
        }

        // 1. aspect->textures->0,1,2,...
        foreach (Match m in AspectTexturesRegex.Matches(sourceCode))
        {
            AddIfNew(new TextureReference
            {
                TextureName = m.Groups[1].Value,
                Source = TextureSourceType.AspectTextures,
                AttributePath = "aspect->textures"
            });
        }

        // 2. aspect->model (implicitná textúra)
        foreach (Match m in AspectModelRegex.Matches(sourceCode))
        {
            AddIfNew(new TextureReference
            {
                TextureName = m.Groups[1].Value,
                Source = TextureSourceType.AspectModel,
                AttributePath = "aspect->model"
            });
        }

        // 3. gui->inventory_icon
        foreach (Match m in InventoryIconRegex.Matches(sourceCode))
        {
            AddIfNew(new TextureReference
            {
                TextureName = m.Groups[1].Value,
                Source = TextureSourceType.InventoryIcon,
                AttributePath = "gui->inventory_icon"
            });
        }

        // 4. Všeobecné atribúty s "texture" v názve
        foreach (Match m in GenericTextureAttrRegex.Matches(sourceCode))
        {
            AddIfNew(new TextureReference
            {
                TextureName = m.Groups[1].Value,
                Source = TextureSourceType.ComponentAttribute,
                AttributePath = m.Value.Split('=')[0].Trim()
            });
        }

        return results;
    }

    /// <summary>
    /// Pokúsi sa nájsť fyzické súbory textúr v /Bits priečinku.
    /// Hľadá .raw a .psd súbory s rovnakým názvom (bez prípony).
    /// </summary>
    public void ResolveTextureFiles(List<TextureReference> textures, string bitsRootPath)
    {
        var allTextureFiles = Directory
            .EnumerateFiles(bitsRootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".raw", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var tex in textures)
        {
            if (allTextureFiles.TryGetValue(tex.TextureName, out var resolvedPath))
                tex.ResolvedPath = resolvedPath;
        }
    }
}
