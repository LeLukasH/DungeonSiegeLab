using DungeonSiegeLab.Models;

namespace DungeonSiegeLab.Services;

public partial class DependencyFinder
{
    private static class AssignmentInterpreterFactory
    {
        public static IExpression Create(
            IReadOnlyDictionary<string, DependencyKind> fixedPropertyRules,
            ISet<string> inventoryDependencySlots)
            => new NonterminalExpression(
                new FixedPropertyExpression(fixedPropertyRules),
                new SpecializesExpression(),
                new AspectTexturesExpression(),
                new AspectVoiceExpression(),
                new AspectVoVoiceExpression(),
                new ConversationExpression(),
                new CommonTriggerExpression(),
                new InventoryExpression(inventoryDependencySlots),
                new GoldRangeExpression(),
                new MagicEnchantmentExpression(),
                new MindJatExpression(),
                new PContentExpression(),
                new PhysicsBreakParticulateExpression(),
                new PotionRangeExpression(),
                new StoreItemRestockExpression());
    }
}
