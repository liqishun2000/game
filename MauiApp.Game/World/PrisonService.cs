using MauiApp.Game.Model;
using MauiApp.Game.Stats;
using MauiApp.Game.World.State;

namespace MauiApp.Game.World;

/// <summary>监狱招降（05 第 9 节）：招降成功转入己方，失败可能逃跑。</summary>
public sealed class PrisonService
{
    private readonly GameState _state;
    private readonly IRandomSource _rng;
    private readonly BalanceConfig _balance;

    public PrisonService(GameState state, IRandomSource rng)
    {
        _state = state;
        _rng = rng;
        _balance = state.Balance;
    }

    /// <summary>尝试招降本方监狱中的某武将。</summary>
    public OperationResult Persuade(string factionId, string prisonerTemplateId)
    {
        if (!_state.Factions.TryGetValue(factionId, out var faction))
            return OperationResult.Fail($"势力不存在: {factionId}");

        var prisoner = faction.Prison.FirstOrDefault(p => p.TemplateId == prisonerTemplateId);
        if (prisoner is null)
            return OperationResult.Fail("监狱中没有该武将");

        int persuaderMeili = _state.TilesOf(factionId)
            .SelectMany(t => t.Generals)
            .Where(g => g.Status == GeneralStatus.Active)
            .Select(g => g.Template.MapStats.Meili)
            .DefaultIfEmpty(0)
            .Max();

        bool loyalist = prisoner.Template.Traits.Contains("sizhong");
        double chance = StatCalculator.PersuadeChance(
            persuaderMeili, prisoner.Template.MapStats.Yizhi, prisoner.DetainedMonths, loyalist, 0, _balance);

        if (_rng.NextDouble() < chance)
        {
            faction.Prison.Remove(prisoner);
            prisoner.FactionId = factionId;
            prisoner.Status = GeneralStatus.Active;
            prisoner.DetainedMonths = 0;

            var capital = _state.TilesOf(factionId).FirstOrDefault();
            if (capital is not null)
            {
                prisoner.TileId = capital.Id;
                capital.Generals.Add(prisoner);
            }

            return OperationResult.Ok($"招降成功：{prisoner.Template.Name} 归顺");
        }

        // 失败：小概率逃跑
        double escapeChance = 0.05 + 0.01 * prisoner.DetainedMonths;
        if (_rng.NextDouble() < escapeChance)
        {
            faction.Prison.Remove(prisoner);
            prisoner.Status = GeneralStatus.Escaped;
            return OperationResult.Fail($"招降失败，且 {prisoner.Template.Name} 越狱逃走");
        }

        return OperationResult.Fail($"招降失败：{prisoner.Template.Name} 不为所动");
    }
}
