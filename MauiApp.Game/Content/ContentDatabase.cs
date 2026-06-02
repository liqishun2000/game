using MauiApp.Game.Model;

namespace MauiApp.Game.Content;

/// <summary>
/// 已加载的全部内容模板，按 id 索引。由 <see cref="ContentLoader"/> 产出。
/// </summary>
public sealed class ContentDatabase
{
    public IReadOnlyDictionary<string, FactionDef> Factions { get; }
    public IReadOnlyDictionary<string, GeneralTemplate> Generals { get; }
    public IReadOnlyDictionary<string, UnitTemplate> Units { get; }
    public IReadOnlyDictionary<string, EquipmentTemplate> Equipment { get; }
    public IReadOnlyDictionary<string, BuildingTemplate> Buildings { get; }
    public IReadOnlyDictionary<string, TechTemplate> Techs { get; }
    public IReadOnlyDictionary<string, MapDef> Maps { get; }

    public ContentDatabase(
        IReadOnlyDictionary<string, FactionDef> factions,
        IReadOnlyDictionary<string, GeneralTemplate> generals,
        IReadOnlyDictionary<string, UnitTemplate> units,
        IReadOnlyDictionary<string, EquipmentTemplate> equipment,
        IReadOnlyDictionary<string, BuildingTemplate> buildings,
        IReadOnlyDictionary<string, TechTemplate> techs,
        IReadOnlyDictionary<string, MapDef> maps)
    {
        Factions = factions;
        Generals = generals;
        Units = units;
        Equipment = equipment;
        Buildings = buildings;
        Techs = techs;
        Maps = maps;
    }
}
