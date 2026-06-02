using MauiApp.Game.Content;
using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.World;

/// <summary>由内容数据库 + 地图定义实例化一局开局 <see cref="GameState"/>。</summary>
public static class GameStateFactory
{
    public static GameState CreateNewGame(
        ContentDatabase content,
        string mapId,
        int seed,
        BalanceConfig? balance = null)
    {
        if (!content.Maps.TryGetValue(mapId, out var map))
            throw new ArgumentException($"地图不存在: {mapId}", nameof(mapId));

        var state = new GameState
        {
            Content = content,
            Balance = balance ?? BalanceConfig.Default,
            Seed = seed,
            Month = 1,
            MapId = mapId,
        };

        foreach (var fdef in content.Factions.Values)
        {
            state.Factions[fdef.Id] = new FactionState
            {
                Id = fdef.Id,
                Def = fdef,
                Gold = fdef.StartResources.Gold,
                Food = fdef.StartResources.Food,
                TechPoints = fdef.StartResources.TechPoints,
            };
        }

        int unitIdSeq = 1;
        foreach (var node in map.Nodes)
        {
            var tile = new TileState
            {
                Id = node.Id,
                Type = node.Type,
                Name = node.Name,
                Col = node.Col,
                Row = node.Row,
                OwnerFactionId = node.OwnerFactionId,
                IsRebelFixed = node.IsRebelFixed,
            };

            foreach (var pb in node.Buildings)
            {
                tile.Buildings.Add(new PlacedBuildingState
                {
                    TemplateId = pb.TemplateId,
                    Template = content.Buildings[pb.TemplateId],
                    Level = pb.Level,
                    RemainingTurns = 0,
                });
            }

            foreach (var gid in node.Garrison.GeneralIds)
            {
                var gt = content.Generals[gid];
                tile.Generals.Add(new GeneralInstance
                {
                    TemplateId = gid,
                    Template = gt,
                    FactionId = node.OwnerFactionId,
                    EquipmentId = gt.DefaultEquipmentId,
                    Status = GeneralStatus.Active,
                    TileId = node.Id,
                });
            }

            foreach (var stack in node.Garrison.Units)
            {
                var ut = content.Units[stack.TemplateId];
                for (int i = 0; i < stack.Count; i++)
                {
                    tile.Units.Add(new UnitInstance
                    {
                        Id = unitIdSeq++,
                        TemplateId = stack.TemplateId,
                        Template = ut,
                        FactionId = node.OwnerFactionId,
                        CurHp = ut.BattleStatsBase.Hp,
                        Morale = 100,
                        TileId = node.Id,
                    });
                }
            }

            state.Tiles[node.Id] = tile;
        }

        foreach (var road in map.Roads)
        {
            if (road.Length != 2) continue;
            if (state.Tiles.TryGetValue(road[0], out var a) && state.Tiles.TryGetValue(road[1], out var b))
            {
                if (!a.Adjacent.Contains(b.Id)) a.Adjacent.Add(b.Id);
                if (!b.Adjacent.Contains(a.Id)) b.Adjacent.Add(a.Id);
            }
        }

        state.NextUnitId = unitIdSeq;
        return state;
    }
}
