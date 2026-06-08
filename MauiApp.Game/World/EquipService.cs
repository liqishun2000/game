using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.World;

/// <summary>武库与武将装备穿戴（唯一装备在武库 / 武将间转移）。</summary>
public sealed class EquipService
{
    private readonly GameState _state;

    public EquipService(GameState state) => _state = state;

    /// <summary>势力武库中未装备的武将专属装备。</summary>
    public IEnumerable<EquipmentTemplate> ArmoryGeneralEquipment(string factionId)
    {
        var faction = _state.Factions[factionId];
        foreach (var id in faction.Armory)
        {
            if (_state.Content.Equipment.TryGetValue(id, out var eq) && eq.ForGeneralOnly)
                yield return eq;
        }
    }

    /// <summary>可为指定武将装备的候选（武库 + 同势力其他武将身上）。</summary>
    public IEnumerable<(EquipmentTemplate Eq, string SourceLabel)> EquipOptionsFor(string factionId, GeneralInstance general)
    {
        var seen = new HashSet<string>();
        foreach (var eq in ArmoryGeneralEquipment(factionId))
        {
            if (seen.Add(eq.Id))
                yield return (eq, "武库");
        }

        foreach (var g in ActiveGenerals(factionId))
        {
            if (g.TemplateId == general.TemplateId || g.EquipmentId is null) continue;
            if (!_state.Content.Equipment.TryGetValue(g.EquipmentId, out var eq) || !eq.ForGeneralOnly) continue;
            if (seen.Add(eq.Id))
                yield return (eq, $"{g.Template.Name} 身上");
        }
    }

    public OperationResult Equip(string factionId, string generalTemplateId, string equipmentId)
    {
        var faction = _state.Factions[factionId];
        var general = FindActiveGeneral(factionId, generalTemplateId);
        if (general is null)
            return OperationResult.Fail("找不到该武将");

        if (!_state.Content.Equipment.TryGetValue(equipmentId, out var eq))
            return OperationResult.Fail("装备不存在");

        if (!eq.ForGeneralOnly)
            return OperationResult.Fail("该装备仅供小兵使用");

        if (eq.RequiredTechId is not null && !faction.ResearchedTechIds.Contains(eq.RequiredTechId))
        {
            var techName = _state.Content.Techs.TryGetValue(eq.RequiredTechId, out var t) ? t.Name : eq.RequiredTechId;
            return OperationResult.Fail($"需要先研究「{techName}」");
        }

        if (general.EquipmentId == equipmentId)
            return OperationResult.Ok($"{general.Template.Name} 已装备 {eq.Name}");

        if (!TryTakeEquipment(faction, equipmentId, general, out var err))
            return OperationResult.Fail(err);

        if (general.EquipmentId is not null)
            ReturnToArmory(faction, general.EquipmentId);

        general.EquipmentId = equipmentId;
        return OperationResult.Ok($"{general.Template.Name} 装备 {eq.Name}");
    }

    public OperationResult Unequip(string factionId, string generalTemplateId)
    {
        var faction = _state.Factions[factionId];
        var general = FindActiveGeneral(factionId, generalTemplateId);
        if (general is null)
            return OperationResult.Fail("找不到该武将");
        if (general.EquipmentId is null)
            return OperationResult.Fail("该武将未装备");

        if (!_state.Content.Equipment.TryGetValue(general.EquipmentId, out var eq))
            return OperationResult.Fail("装备数据异常");

        ReturnToArmory(faction, general.EquipmentId);
        general.EquipmentId = null;
        return OperationResult.Ok($"{general.Template.Name} 卸下 {eq.Name}，已收入武库");
    }

    public static string DescribeEquipment(EquipmentTemplate eq)
    {
        var mods = new List<string>();
        if (eq.StatMods.PAtk != 0) mods.Add($"物攻+{eq.StatMods.PAtk}");
        if (eq.StatMods.PDef != 0) mods.Add($"物防+{eq.StatMods.PDef}");
        if (eq.StatMods.MAtk != 0) mods.Add($"魔攻+{eq.StatMods.MAtk}");
        if (eq.StatMods.MDef != 0) mods.Add($"魔防+{eq.StatMods.MDef}");
        if (eq.StatMods.Spd != 0) mods.Add($"速度+{eq.StatMods.Spd}");
        if (eq.StatMods.Hp != 0) mods.Add($"生命+{eq.StatMods.Hp}");
        string modText = mods.Count > 0 ? string.Join(" ", mods) : "无属性加成";
        string fx = eq.Effects.Count > 0 ? $"\n特效：{string.Join("、", eq.Effects)}" : "";
        string tag = eq.IsUnique ? "唯一" : "可量产";
        return $"{modText}（{tag}）{fx}";
    }

    private IEnumerable<GeneralInstance> ActiveGenerals(string factionId) =>
        _state.TilesOf(factionId).SelectMany(t => t.Generals).Where(g => g.Status == GeneralStatus.Active);

    private GeneralInstance? FindActiveGeneral(string factionId, string templateId) =>
        ActiveGenerals(factionId).FirstOrDefault(g => g.TemplateId == templateId);

    private GeneralInstance? FindHolder(string factionId, string equipmentId) =>
        ActiveGenerals(factionId).FirstOrDefault(g => g.EquipmentId == equipmentId);

    private bool TryTakeEquipment(FactionState faction, string equipmentId, GeneralInstance target, out string error)
    {
        if (faction.Armory.Remove(equipmentId))
        {
            error = "";
            return true;
        }

        var holder = FindHolder(faction.Id, equipmentId);
        if (holder is null)
        {
            error = "武库中没有该装备";
            return false;
        }

        if (holder.TemplateId != target.TemplateId)
            holder.EquipmentId = null;

        error = "";
        return true;
    }

    private void ReturnToArmory(FactionState faction, string equipmentId)
    {
        if (!_state.Content.Equipment.TryGetValue(equipmentId, out var eq) || !eq.IsUnique) return;
        if (!faction.Armory.Contains(equipmentId))
            faction.Armory.Add(equipmentId);
    }
}
