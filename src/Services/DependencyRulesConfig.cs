namespace DungeonSiegeLab.Services;

/// Editable rule set loaded from dependency-rules.json.
/// Any missing values are filled from CreateDefault().
public class DependencyRulesConfig
{
    /// Top-level component blocks considered vanilla, not custom dependencies.
    public List<string> VanillaBlocks { get; set; } = new();

    /// Inventory slot properties that should be interpreted as template dependencies.
    public List<string> InventoryDependencySlots { get; set; } = new();

    /// Exact property map in root:property format, e.g. "gui:inventory_icon" -> "Texture".
    /// Stored as string in JSON and parsed into DependencyKind at runtime.
    public Dictionary<string, string> FixedPropertyKinds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// Baseline rules that match current known behavior.
    /// Used both as fallback and as merge source for partially specified JSON files.
    public static DependencyRulesConfig CreateDefault()
    {
        return new DependencyRulesConfig
        {
            VanillaBlocks = new List<string>
            {
                "actor", "aspect", "attack", "body", "common", "conversation", "defend", "follower",
                "gizmo", "gold", "gui", "inventory", "magic", "mind", "party", "pcontent", "physics",
                "placement", "potion", "spell", "store"
            },
            InventoryDependencySlots = new List<string>
            {
                "il_main", "es_head", "es_chest", "es_forearms", "es_feet", "es_weapon_hand", "es_shield_hand",
                "il_active_primary_spell", "il_active_secondary_spell", "il_spell_1", "il_spell_2", "il_spell_3",
                "il_spell_4", "il_spell_5", "il_spell_6", "il_spell_7", "il_spell_8", "il_spell_9", "il_spell_10",
                "il_spell_11", "il_spell_12", "es_amulet", "es_ring_1", "es_ring_2", "es_ring_3", "es_ring_4", "es_spellbook"
            },
            FixedPropertyKinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["actor:portrait_icon"] = "Texture",
                ["aspect:expired_template_name"] = "Template",
                ["aspect:material"] = "Texture",
                ["aspect:megamap_icon"] = "Texture",
                ["aspect:model"] = "Texture",
                ["attack:ammo_template"] = "Template",
                ["common:membership"] = "Template",
                ["defend:armor_style"] = "Template",
                ["defend:armor_type"] = "Template",
                ["gui:inventory_icon"] = "Texture",
                ["gui:active_icon"] = "Texture",
                ["gui:lore_key"] = "Template",
                ["gizmo:model"] = "Texture",
                ["gizmo:texture"] = "Texture",
                ["inventory:custom_head"] = "Texture",
                ["mind:comm_channels"] = "Template",
                ["physics:fire_effect"] = "Effect",
                ["physics:fire_charred_template"] = "Template",
                ["physics:break_effect"] = "Effect",
                ["physics:break_sound"] = "Sound",
                ["potion:inventory_icon_2"] = "Texture"
            }
        };
    }
}
