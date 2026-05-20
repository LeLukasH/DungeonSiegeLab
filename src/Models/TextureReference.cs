namespace DungeonSiegeLab.Models;

public enum TextureSourceType
{
    AspectTextures,     // aspect->textures->0,1,2,...
    AspectModel,        // implicit texture z aspect->model
    InventoryIcon,      // gui->inventory_icon
    ComponentAttribute  // akýkoľvek atribút obsahujúci "texture"
}

public class TextureReference
{
    public string TextureName { get; init; } = "";
    public TextureSourceType Source { get; init; }
    public string AttributePath { get; init; } = "";

    /// <summary>Absolútna cesta k súboru textúry (ak bol nájdený v /Bits).</summary>
    public string? ResolvedPath { get; set; }

    /// <summary>True ak textúra je implicitná (odvodená z model názvu).</summary>
    public bool IsImplicit => Source == TextureSourceType.AspectModel;

    public override string ToString() => TextureName;
}
