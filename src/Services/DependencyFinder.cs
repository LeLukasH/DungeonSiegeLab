using System.Text.Json;
using System.Text.RegularExpressions;
using DungeonSiegeLab.Models;
using DungeonSiegeLab.Patterns;

namespace DungeonSiegeLab.Services;

/// Unified dependency engine for template analysis.
/// It resolves local dependencies, follows specializes recursively, and marks inherited items.
public partial class DependencyFinder
{
    private readonly HashSet<string> _vanillaBlocks;
    private readonly HashSet<string> _inventoryDependencySlots;
    private readonly Dictionary<string, DependencyKind> _fixedPropertyRules;
    private readonly IExpression _assignmentInterpreter;

    private static readonly Regex BlockHeaderRegex = new(@"\[(?<name>[^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex AssignmentRegex = new(
        @"(?<key>[\?&]?[A-Za-z_][\w\-\*:]*)\s*=\s*(?<value>[^;{}]+)",
        RegexOptions.Compiled);
    private static readonly Regex ModelPosSuffixRegex = new(@"_pos(?:[_-]\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// PATTERN
    public DependencyFinder()
    {
        // Load user-editable rules once; use defaults if file is missing or invalid.
        var rules = LoadRulesConfig();

        _vanillaBlocks = new HashSet<string>(rules.VanillaBlocks, StringComparer.OrdinalIgnoreCase);
        _inventoryDependencySlots = new HashSet<string>(rules.InventoryDependencySlots, StringComparer.OrdinalIgnoreCase);
        _fixedPropertyRules = rules.FixedPropertyKinds
            .ToDictionary(kvp => kvp.Key, kvp => ParseKindOrDefault(kvp.Value), StringComparer.OrdinalIgnoreCase);
        _assignmentInterpreter = BuildAssignmentInterpreter();
    }

    public List<DependencyReference> IdentifyDependencies(
        BitsTemplate template,
        IReadOnlyDictionary<string, BitsTemplate> templateIndex)
    {
        // Cache prevents repeated work when multiple nodes share ancestors.
        var cache = new Dictionary<string, AnalyzeResult>(StringComparer.OrdinalIgnoreCase);
        // Visiting set prevents infinite loops if template graph is malformed/cyclic.
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = AnalyzeTemplateRecursive(template, templateIndex, cache, visiting);

        return result.Dependencies
            .OrderBy(d => d.IsInherited)
            .ThenBy(d => d.Line)
            .ThenBy(d => d.Kind)
            .ThenBy(d => d.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// PATTERN
    private AnalyzeResult AnalyzeTemplateRecursive(
        BitsTemplate template,
        IReadOnlyDictionary<string, BitsTemplate> templateIndex,
        Dictionary<string, AnalyzeResult> cache,
        HashSet<string> visiting)
    {
        if (cache.TryGetValue(template.TemplateName, out var cached))
            return cached;

        if (!visiting.Add(template.TemplateName))
            return new AnalyzeResult();

        var parsed = ParseTemplate(template);
        var localDependencies = ExtractLocalDependencies(template, parsed);
        var combined = new AnalyzeResult();
        combined.LocalSignatures.UnionWith(parsed.LocalSignatures);
        combined.Dependencies.AddRange(localDependencies);

        if (!string.IsNullOrWhiteSpace(parsed.Specializes)
            && templateIndex.TryGetValue(parsed.Specializes, out var parentTemplate))
        {
            var parent = AnalyzeTemplateRecursive(parentTemplate, templateIndex, cache, visiting);

            EnumerationUtility.Enumerate(parent.Dependencies, dep =>
            {
                // Local assignment with same signature overrides inherited source.
                if (!string.IsNullOrEmpty(dep.SourceSignature)
                    && parsed.LocalSignatures.Contains(dep.SourceSignature))
                {
                    return;
                }

                combined.Dependencies.Add(new DependencyReference
                {
                    Value = dep.Value,
                    Kind = dep.Kind,
                    Rule = dep.Rule,
                    SourcePath = dep.SourcePath,
                    Line = dep.Line,
                    IsInherited = true,
                    SourceTemplate = dep.SourceTemplate,
                    SourceSignature = dep.SourceSignature
                });
            });
        }

        visiting.Remove(template.TemplateName);

        var deduped = Deduplicate(combined.Dependencies);
        combined.Dependencies.Clear();
        combined.Dependencies.AddRange(deduped);

        cache[template.TemplateName] = combined;
        return combined;
    }

    /// PATTERN: Interpreter - parses template text into structured assignment records.
    private ParseResult ParseTemplate(BitsTemplate template)
    {
        var result = new ParseResult();
        var stack = new List<string>();
        var pendingBlocks = new Queue<string>();

        bool inBlockComment = false;
        // The first '{' in the source is the template body itself — we skip it so the
        // stack is empty while we're at the template top level, and depth-1 when inside
        // a component block. This makes path lookups like "aspect:model" correct.
        bool templateBodyEntered = false;
        var lines = template.SourceCode.Replace("\r\n", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var rawLine = lines[i];
            var line = StripComments(rawLine, ref inBlockComment);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            foreach (Match m in BlockHeaderRegex.Matches(line))
            {
                var raw = m.Groups["name"].Value.Trim();
                if (raw.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var block = NormalizeBlockName(raw);
                if (!string.IsNullOrWhiteSpace(block))
                    pendingBlocks.Enqueue(block);
            }

            var openCount = line.Count(ch => ch == '{');
            for (int open = 0; open < openCount; open++)
            {
                if (!templateBodyEntered)
                {
                    // Skip the template's own outer brace; consume any pending header for it.
                    templateBodyEntered = true;
                    if (pendingBlocks.Count > 0)
                        pendingBlocks.Dequeue();
                    continue;
                }

                var block = pendingBlocks.Count > 0 ? pendingBlocks.Dequeue() : "(anonymous)";
                stack.Add(block);

                // Only flag non-vanilla blocks at the top component level (depth 1).
                // Nested sub-blocks like bone_translator, chore_dictionary, anim_files
                // are implementation details of a vanilla component, not dependencies.
                if (stack.Count == 1 && !IsVanillaBlock(block))
                {
                    var path = string.Join(":", stack);
                    result.NonVanillaBlockDependencies.Add(new DependencyReference
                    {
                        Value = path,
                        Kind = DependencyKind.Component,
                        Rule = "non-vanilla-block",
                        SourcePath = path,
                        Line = lineNumber,
                        SourceTemplate = template.TemplateName,
                        SourceSignature = $"block:{path}"
                    });
                }
            }

            foreach (Match m in AssignmentRegex.Matches(line))
            {
                var key = NormalizePropertyKey(m.Groups["key"].Value);
                var value = m.Groups["value"].Value.Trim();
                var path = string.Join(":", stack);
                var signature = string.IsNullOrEmpty(path)
                    ? key
                    : $"{path}:{key}";

                result.Assignments.Add(new AssignmentRecord
                {
                    Path = path,
                    Key = key,
                    Value = value,
                    Line = lineNumber,
                    Signature = signature
                });
                result.LocalSignatures.Add(signature);

                // specializes is the parent link used for recursive inheritance.
                if (key.Equals("specializes", StringComparison.OrdinalIgnoreCase))
                    result.Specializes = ExtractValueTokens(value).FirstOrDefault();

                // aspect:model may imply texture when explicit aspect:textures is missing.
                if (PathStartsWith(path, "aspect") && key.Equals("model", StringComparison.OrdinalIgnoreCase))
                    result.AspectModel = ExtractValueTokens(value).FirstOrDefault();

                if (PathStartsWith(path, "aspect") && key.StartsWith("textures:", StringComparison.OrdinalIgnoreCase))
                    result.HasExplicitAspectTexture = true;
            }

            var closeCount = line.Count(ch => ch == '}');
            for (int close = 0; close < closeCount && stack.Count > 0; close++)
                stack.RemoveAt(stack.Count - 1);
        }

        return result;
    }

    /// PATTERN: Interpreter - terminal/nonterminal expressions evaluate assignment grammar rules.
    private List<DependencyReference> ExtractLocalDependencies(BitsTemplate template, ParseResult parsed)
    {
        var dependencies = new List<DependencyReference>();
        // Custom top-level components are dependencies by themselves.
        dependencies.AddRange(parsed.NonVanillaBlockDependencies);

        EnumerationUtility.Enumerate(parsed.Assignments, a =>
        {
            _assignmentInterpreter.Interpret(new DependencyInterpretContext
            {
                TemplateName = template.TemplateName,
                Dependencies = dependencies,
                Assignment = a
            });
        });

        if (!parsed.HasExplicitAspectTexture && !string.IsNullOrWhiteSpace(parsed.AspectModel))
        {
            // Legacy game convention: model name implies texture prefix when no explicit texture exists.
            var inferred = InferTextureNameFromModel(parsed.AspectModel);
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                dependencies.Add(new DependencyReference
                {
                    Value = inferred,
                    Kind = DependencyKind.Texture,
                    Rule = "aspect:model:implicit",
                    SourcePath = "aspect:model",
                    Line = 0,
                    SourceTemplate = template.TemplateName,
                    SourceSignature = "aspect:model"
                });
            }
        }

        return Deduplicate(dependencies);
    }

    private IExpression BuildAssignmentInterpreter()
        => AssignmentInterpreterFactory.Create(_fixedPropertyRules, _inventoryDependencySlots);

    private static void AddTokens(
        List<DependencyReference> deps,
        string templateName,
        AssignmentRecord assignment,
        DependencyKind kind,
        string rule,
        string value)
    {
        foreach (var token in ExtractValueTokens(value))
            AddToken(deps, templateName, assignment, kind, rule, token);
    }


    private static void AddToken(
        List<DependencyReference> deps,
        string templateName,
        AssignmentRecord assignment,
        DependencyKind kind,
        string rule,
        string token)
    {
        token = NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(token))
            return;

        deps.Add(new DependencyReference
        {
            Value = token,
            Kind = kind,
            Rule = rule,
            SourcePath = string.IsNullOrEmpty(assignment.Path)
                ? assignment.Key
                : $"{assignment.Path}:{assignment.Key}",
            Line = assignment.Line,
            SourceTemplate = templateName,
            SourceSignature = assignment.Signature
        });
    }

    private static string NormalizePropertyKey(string key)
        => key.Trim().TrimStart('?', '&');

    private static string NormalizeBlockName(string raw)
    {
        var first = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? raw;
        return first.Trim();
    }

    private bool IsVanillaBlock(string block)
    {
        var name = block.Trim().TrimEnd('*');
        return _vanillaBlocks.Contains(name);
    }

    /// PATTERN: Strategy/Configuration - load runtime rule set with safe fallback.
    private static DependencyRulesConfig LoadRulesConfig()
    {
        var defaults = DependencyRulesConfig.CreateDefault();
        var configPath = Path.Combine(AppContext.BaseDirectory, "dependency-rules.json");
        if (!File.Exists(configPath))
            return defaults;

        try
        {
            var json = File.ReadAllText(configPath);
            var loaded = JsonSerializer.Deserialize<DependencyRulesConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (loaded is null)
                return defaults;

            return MergeWithDefaults(defaults, loaded);
        }
        catch
        {
            return defaults;
        }
    }

    private static DependencyRulesConfig MergeWithDefaults(DependencyRulesConfig defaults, DependencyRulesConfig loaded)
    {
        // Merge is additive: user file can override only what it cares about.
        var merged = DependencyRulesConfig.CreateDefault();

        if (loaded.VanillaBlocks.Count > 0)
            merged.VanillaBlocks = loaded.VanillaBlocks;

        if (loaded.InventoryDependencySlots.Count > 0)
            merged.InventoryDependencySlots = loaded.InventoryDependencySlots;

        foreach (var rule in loaded.FixedPropertyKinds)
            merged.FixedPropertyKinds[rule.Key] = rule.Value;

        return merged;
    }

    private static DependencyKind ParseKindOrDefault(string value)
    {
        // Unknown string kinds degrade safely into Other instead of throwing.
        if (Enum.TryParse<DependencyKind>(value, ignoreCase: true, out var parsed))
            return parsed;
        return DependencyKind.Other;
    }

    private static string GetRootComponent(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        var first = path.Split(':', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return first.Trim().TrimEnd('*');
    }

    private static bool PathStartsWith(string path, string prefix)
        => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool PathContains(string path, string fragment)
        => path.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static List<string> ExtractValueTokens(string value)
    {
        // Tokenization is intentionally permissive for legacy GAS value formats.
        var tokens = value
            .Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeToken)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return tokens;
    }

    private static string NormalizeToken(string token)
    {
        // Filter out placeholders, booleans, and pure numerics that are not real dependencies.
        var t = token.Trim().Trim('"', '\'', ';');
        if (string.IsNullOrWhiteSpace(t)) return "";
        if (t.Equals("<ignore>", StringComparison.OrdinalIgnoreCase)) return "";
        if (t.Equals("true", StringComparison.OrdinalIgnoreCase)) return "";
        if (t.Equals("false", StringComparison.OrdinalIgnoreCase)) return "";
        if (double.TryParse(t, out _)) return "";
        return t;
    }

    private static string StripComments(string line, ref bool inBlockComment)
    {
        // Strips // and /* */ comments before regex parsing to reduce false positives.
        if (string.IsNullOrEmpty(line))
            return line;

        var work = line;
        if (inBlockComment)
        {
            var end = work.IndexOf("*/", StringComparison.Ordinal);
            if (end < 0) return "";
            work = work[(end + 2)..];
            inBlockComment = false;
        }

        while (true)
        {
            var start = work.IndexOf("/*", StringComparison.Ordinal);
            if (start < 0) break;
            var end = work.IndexOf("*/", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                work = work[..start];
                inBlockComment = true;
                break;
            }
            work = work.Remove(start, end - start + 2);
        }

        var slashComment = work.IndexOf("//", StringComparison.Ordinal);
        if (slashComment >= 0)
            work = work[..slashComment];

        return work;
    }

    private static string? ExtractFunctionArgument(string expression, int argIndex)
    {
        var open = expression.IndexOf('(');
        var close = expression.LastIndexOf(')');
        if (open < 0 || close <= open)
            return null;

        var inner = expression[(open + 1)..close];
        var args = inner.Split(',', StringSplitOptions.TrimEntries);
        if (argIndex < 0 || argIndex >= args.Length)
            return null;

        return NormalizeToken(args[argIndex]);
    }

    private static DependencyKind InferKind(string token)
    {
        if (token.Contains("sound", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("s_", StringComparison.OrdinalIgnoreCase))
            return DependencyKind.Sound;

        if (token.Contains("effect", StringComparison.OrdinalIgnoreCase))
            return DependencyKind.Effect;

        if (token.StartsWith("b_", StringComparison.OrdinalIgnoreCase)
            || token.Contains("texture", StringComparison.OrdinalIgnoreCase)
            || token.Contains("icon", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("m_", StringComparison.OrdinalIgnoreCase))
            return DependencyKind.Texture;

        if (token.EndsWith(".skrit", StringComparison.OrdinalIgnoreCase))
            return DependencyKind.Script;

        return DependencyKind.Template;
    }

    private static string InferTextureNameFromModel(string modelName)
    {
        var model = NormalizeToken(modelName);
        if (string.IsNullOrWhiteSpace(model)) return "";

        model = ModelPosSuffixRegex.Replace(model, "");
        if (model.StartsWith("m_", StringComparison.OrdinalIgnoreCase))
            model = "b_" + model[2..];

        return model;
    }

    private static List<DependencyReference> Deduplicate(IEnumerable<DependencyReference> dependencies)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Enumeration-method style: filter stream by first-seen identity key.
        return dependencies
            .Where(dep => seen.Add(string.Join("|",
                dep.Kind,
                dep.Value,
                dep.Rule,
                dep.SourcePath,
                dep.Line,
                dep.IsInherited,
                dep.SourceTemplate)))
            .ToList();
    }
}
