using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private sealed class FixedPropertyExpression : TerminalExpression
    {
        private readonly IReadOnlyDictionary<string, DependencyKind> _fixedPropertyRules;

        public FixedPropertyExpression(IReadOnlyDictionary<string, DependencyKind> fixedPropertyRules)
        {
            _fixedPropertyRules = fixedPropertyRules;
        }

        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            var root = GetRootComponent(a.Path);
            if (!string.IsNullOrEmpty(root)
                && _fixedPropertyRules.TryGetValue($"{root}:{a.Key}", out var fixedKind))
            {
                AddTokens(context.Dependencies, context.TemplateName, a, fixedKind, $"fixed:{root}:{a.Key}", a.Value);
            }
        }
    }

    private sealed class SpecializesExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (a.Key.Equals("specializes", StringComparison.OrdinalIgnoreCase))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Template, "specializes", a.Value);
        }
    }

    private sealed class AspectTexturesExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "aspect") && a.Key.StartsWith("textures:", StringComparison.OrdinalIgnoreCase))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Texture, "aspect:textures", a.Value);
        }
    }

    private sealed class AspectVoiceExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "aspect:voice") && a.Key.Equals("*", StringComparison.OrdinalIgnoreCase))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Sound, "aspect:voice:*", a.Value);
        }
    }

    private sealed class AspectVoVoiceExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "aspect:vo_voice") && a.Key.Equals("*", StringComparison.OrdinalIgnoreCase))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Sound, "aspect:vo_voice:*", a.Value);
        }
    }

    private sealed class ConversationExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "conversation:conversations") && a.Key.Equals("*", StringComparison.OrdinalIgnoreCase))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Template, "conversation:conversations:*", a.Value);
        }
    }

    private sealed class CommonTriggerExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (!PathStartsWith(a.Path, "common:instance_triggers")
                && !PathStartsWith(a.Path, "common:template_triggers"))
            {
                return;
            }

            if (a.Key.StartsWith("action", StringComparison.OrdinalIgnoreCase)
                && a.Value.Contains("call_sfx_script", StringComparison.OrdinalIgnoreCase))
            {
                var scriptArg = ExtractFunctionArgument(a.Value, 0);
                if (!string.IsNullOrWhiteSpace(scriptArg))
                    AddToken(context.Dependencies, context.TemplateName, a, DependencyKind.Script, "common:trigger:action:call_sfx_script", scriptArg);
            }

            if (!a.Key.StartsWith("condition", StringComparison.OrdinalIgnoreCase))
                return;

            if (a.Value.Contains("has_go_in_inventory", StringComparison.OrdinalIgnoreCase)
                || a.Value.Contains("go_within_range", StringComparison.OrdinalIgnoreCase)
                || a.Value.Contains("go_within_bounding_box", StringComparison.OrdinalIgnoreCase)
                || a.Value.Contains("go_within_sphere", StringComparison.OrdinalIgnoreCase))
            {
                var second = ExtractFunctionArgument(a.Value, 1);
                var third = ExtractFunctionArgument(a.Value, 2);
                if (!string.IsNullOrWhiteSpace(second))
                    AddToken(context.Dependencies, context.TemplateName, a, InferKind(second), "common:trigger:condition:arg2", second);
                if (!string.IsNullOrWhiteSpace(third))
                    AddToken(context.Dependencies, context.TemplateName, a, InferKind(third), "common:trigger:condition:arg3", third);
            }
        }
    }

    private sealed class InventoryExpression : TerminalExpression
    {
        private readonly ISet<string> _inventoryDependencySlots;

        public InventoryExpression(ISet<string> inventoryDependencySlots)
        {
            _inventoryDependencySlots = inventoryDependencySlots;
        }

        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (!PathStartsWith(a.Path, "inventory"))
                return;

            if (_inventoryDependencySlots.Contains(a.Key)
                && !a.Value.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Template, "inventory:slot", a.Value);
            }

            if (PathStartsWith(a.Path, "inventory:ranges"))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Template, "inventory:ranges:*", a.Value);
        }
    }

    private sealed class GoldRangeExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "gold:ranges"))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Template, "gold:ranges:*", a.Value);
        }
    }

    private sealed class MagicEnchantmentExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "magic:enchantments")
                && (a.Key.Equals("effect_script", StringComparison.OrdinalIgnoreCase)
                    || a.Key.Equals("effect_script_equip", StringComparison.OrdinalIgnoreCase)
                    || a.Key.Equals("effect_script_hit", StringComparison.OrdinalIgnoreCase)))
            {
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Script, "magic:enchantments:effect_script", a.Value);
            }
        }
    }

    private sealed class MindJatExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "mind") && a.Key.StartsWith("jat_", StringComparison.OrdinalIgnoreCase))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Script, "mind:jat_*", a.Value);
        }
    }

    private sealed class PContentExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathContains(a.Path, "pcontent")
                && (a.Key.Equals("inventory_icon", StringComparison.OrdinalIgnoreCase)
                    || a.Key.Equals("model", StringComparison.OrdinalIgnoreCase)
                    || a.Key.Equals("texture", StringComparison.OrdinalIgnoreCase)))
            {
                var kind = a.Key.Equals("model", StringComparison.OrdinalIgnoreCase)
                    ? DependencyKind.Template
                    : DependencyKind.Texture;
                AddTokens(context.Dependencies, context.TemplateName, a, kind, "pcontent:*:[inventory_icon|model|texture]", a.Value);
            }
        }
    }

    private sealed class PhysicsBreakParticulateExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "physics:break_particulate"))
            {
                AddToken(context.Dependencies, context.TemplateName, a, DependencyKind.Effect,
                    "physics:break_particulate:left_side", a.Key);
            }
        }
    }

    private sealed class PotionRangeExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "potion:ranges"))
                AddTokens(context.Dependencies, context.TemplateName, a, DependencyKind.Template, "potion:ranges:*", a.Value);
        }
    }

    private sealed class StoreItemRestockExpression : TerminalExpression
    {
        public override void Interpret(DependencyInterpretContext context)
        {
            var a = context.Assignment;
            if (PathStartsWith(a.Path, "store:item_restock"))
            {
                AddToken(context.Dependencies, context.TemplateName, a, DependencyKind.Template,
                    "store:item_restock:left_side", a.Key);
            }
        }
    }
}
