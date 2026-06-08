using System.Text.Json;
using System.Text.Json.Serialization;
using MauiApp.Game.Content;
using MauiApp.Game.World;
using MauiApp.Game.World.State;

namespace MauiApp.Game.Save;

/// <summary>存档读写：把 GameState 序列化为可变状态 DTO，并在读取时用内容库重建对象图。</summary>
public static class SaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    public static string Serialize(GameState state)
    {
        var dto = new SaveData
        {
            Seed = state.Seed,
            MapId = state.MapId,
            Month = state.Month,
            NextUnitId = state.NextUnitId,
            Difficulty = state.Difficulty,
        };

        foreach (var f in state.Factions.Values)
        {
            dto.Factions.Add(new SaveFaction
            {
                Id = f.Id,
                Gold = f.Gold,
                Food = f.Food,
                TechPoints = f.TechPoints,
                Researched = f.ResearchedTechIds.ToList(),
                Armory = f.Armory.ToList(),
                Prison = f.Prison.Select(ToGeneralDto).ToList(),
            });
        }

        foreach (var t in state.Tiles.Values)
        {
            dto.Tiles.Add(new SaveTile
            {
                Id = t.Id,
                OwnerFactionId = t.OwnerFactionId,
                IsRebelFixed = t.IsRebelFixed,
                Buildings = t.Buildings.Select(b => new SaveBuilding
                {
                    TemplateId = b.TemplateId, Level = b.Level, RemainingTurns = b.RemainingTurns,
                }).ToList(),
                Generals = t.Generals.Select(ToGeneralDto).ToList(),
                Units = t.Units.Select(u => new SaveUnit
                {
                    Id = u.Id, TemplateId = u.TemplateId, FactionId = u.FactionId,
                    OwnerGeneralId = u.OwnerGeneralId, EquipmentId = u.EquipmentId,
                    CurHp = u.CurHp, Morale = u.Morale, TileId = u.TileId,
                }).ToList(),
            });
        }

        return JsonSerializer.Serialize(dto, Options);
    }

    public static GameState Deserialize(string json, ContentDatabase content)
    {
        var dto = JsonSerializer.Deserialize<SaveData>(json, Options)
                  ?? throw new InvalidOperationException("存档解析失败");

        // 用地图结构重建骨架（Col/Row/Adjacent/Type/Name），再覆盖可变状态。
        var state = GameStateFactory.CreateNewGame(content, dto.MapId, dto.Seed);
        state.Month = dto.Month;
        state.NextUnitId = dto.NextUnitId;
        state.Difficulty = dto.Difficulty;

        foreach (var fd in dto.Factions)
        {
            if (!state.Factions.TryGetValue(fd.Id, out var f)) continue;
            f.Gold = fd.Gold;
            f.Food = fd.Food;
            f.TechPoints = fd.TechPoints;

            f.ResearchedTechIds.Clear();
            foreach (var id in fd.Researched) f.ResearchedTechIds.Add(id);

            f.Armory.Clear();
            f.Armory.AddRange(fd.Armory);

            f.Prison.Clear();
            f.Prison.AddRange(fd.Prison.Select(g => FromGeneralDto(g, content)));
        }

        foreach (var td in dto.Tiles)
        {
            if (!state.Tiles.TryGetValue(td.Id, out var t)) continue;
            t.OwnerFactionId = td.OwnerFactionId;
            t.IsRebelFixed = td.IsRebelFixed;

            t.Buildings.Clear();
            foreach (var b in td.Buildings)
                t.Buildings.Add(new PlacedBuildingState
                {
                    TemplateId = b.TemplateId, Template = content.Buildings[b.TemplateId],
                    Level = b.Level, RemainingTurns = b.RemainingTurns,
                });

            t.Generals.Clear();
            t.Generals.AddRange(td.Generals.Select(g => FromGeneralDto(g, content)));

            t.Units.Clear();
            foreach (var u in td.Units)
                t.Units.Add(new UnitInstance
                {
                    Id = u.Id, TemplateId = u.TemplateId, Template = content.Units[u.TemplateId],
                    FactionId = u.FactionId, OwnerGeneralId = u.OwnerGeneralId, EquipmentId = u.EquipmentId,
                    CurHp = u.CurHp, Morale = u.Morale, TileId = u.TileId,
                });
        }

        return state;
    }

    private static SaveGeneral ToGeneralDto(GeneralInstance g) => new()
    {
        TemplateId = g.TemplateId, FactionId = g.FactionId, Level = g.Level, Exp = g.Exp,
        EquipmentId = g.EquipmentId, Status = g.Status, TileId = g.TileId, DetainedMonths = g.DetainedMonths,
        ActedThisMonth = g.ActedThisMonth,
    };

    private static GeneralInstance FromGeneralDto(SaveGeneral g, ContentDatabase content) => new()
    {
        TemplateId = g.TemplateId, Template = content.Generals[g.TemplateId], FactionId = g.FactionId,
        Level = g.Level, Exp = g.Exp, EquipmentId = g.EquipmentId, Status = g.Status,
        TileId = g.TileId, DetainedMonths = g.DetainedMonths, ActedThisMonth = g.ActedThisMonth,
    };
}
