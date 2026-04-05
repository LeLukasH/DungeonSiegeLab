using System.Text.RegularExpressions;
using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

/// <summary>
/// Parsuje Dungeon Siege .gas súbory a extrahuje templates.
/// .gas formát: [t:template,n:meno_template] { ... }
/// </summary>
public class GasParser
{
    // Nájde začiatok template bloku: [t:template,n:nazov]
    private static readonly Regex TemplateHeaderRegex = new(
        @"\[\s*t\s*:\s*template\s*,\s*n\s*:\s*(\w+)\s*\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Načíta .gas súbor a vráti zoznam všetkých templates.
    /// Každý template obsahuje meno a kompletný zdrojový kód bloku.
    /// </summary>
    public async Task<List<BitsTemplate>> ParseFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return ParseContent(content, filePath);
    }

    public List<BitsTemplate> ParseContent(string content, string filePath)
    {
        var templates = new List<BitsTemplate>();
        var matches = TemplateHeaderRegex.Matches(content);

        foreach (Match match in matches)
        {
            var templateName = match.Groups[1].Value;
            var blockStart = match.Index;
            var sourceCode = ExtractBlock(content, blockStart);

            templates.Add(new BitsTemplate
            {
                Name = templateName,
                FullPath = filePath,
                TemplateName = templateName,
                SourceCode = sourceCode
            });
        }

        return templates;
    }

    /// <summary>
    /// Extrahuje celý { ... } blok začínajúci od danej pozície.
    /// Sleduje vnorenie { } pre správne ukončenie.
    /// </summary>
    private static string ExtractBlock(string content, int startIndex)
    {
        // Nájdi prvú { za startIndex
        var braceStart = content.IndexOf('{', startIndex);
        if (braceStart < 0)
            return content[startIndex..];

        // Vezmi aj header (riadok pred {)
        var headerStart = content.LastIndexOf('\n', braceStart) + 1;

        int depth = 0;
        for (int i = braceStart; i < content.Length; i++)
        {
            if (content[i] == '{') depth++;
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return content[headerStart..(i + 1)];
            }
        }

        // Nedokončený blok – vráť zvyšok
        return content[headerStart..];
    }
}
