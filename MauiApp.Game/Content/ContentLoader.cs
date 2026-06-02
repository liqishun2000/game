using System.Text.Json;
using System.Text.Json.Serialization;
using MauiApp.Game.Model;

namespace MauiApp.Game.Content;

/// <summary>各内容文件的原始 JSON 文本（MAUI 端从 Raw 资源读出后填入，单测/控制台从目录读取）。</summary>
public sealed class ContentJsonSources
{
    public string? Factions { get; set; }
    public string? Generals { get; set; }
    public string? Units { get; set; }
    public string? Equipment { get; set; }
    public string? Buildings { get; set; }
    public string? Techs { get; set; }
    public List<string> Maps { get; } = new();
}

/// <summary>加载结果：数据库 + 校验结果。</summary>
public sealed class ContentLoadResult
{
    public required ContentDatabase Database { get; init; }
    public required ContentValidationResult Validation { get; init; }
    public bool Success => Validation.IsValid;
}

/// <summary>
/// 内容加载器：把 JSON 解析为模板并做引用完整性校验。
/// 不依赖 MAUI，便于单元测试与控制台模拟（01-architecture.md / 02-data-model.md）。
/// </summary>
public sealed class ContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) },
    };

    /// <summary>从目录加载：root/data/*.json 与 root/maps/*.json。</summary>
    public ContentLoadResult LoadFromDirectory(string root)
    {
        string dataDir = Path.Combine(root, "data");
        string mapsDir = Path.Combine(root, "maps");

        var sources = new ContentJsonSources
        {
            Factions = ReadIfExists(Path.Combine(dataDir, "factions.json")),
            Generals = ReadIfExists(Path.Combine(dataDir, "generals.json")),
            Units = ReadIfExists(Path.Combine(dataDir, "units.json")),
            Equipment = ReadIfExists(Path.Combine(dataDir, "equipment.json")),
            Buildings = ReadIfExists(Path.Combine(dataDir, "buildings.json")),
            Techs = ReadIfExists(Path.Combine(dataDir, "tech.json")),
        };

        if (Directory.Exists(mapsDir))
        {
            foreach (var file in Directory.EnumerateFiles(mapsDir, "*.json").OrderBy(f => f))
            {
                sources.Maps.Add(File.ReadAllText(file));
            }
        }

        return Load(sources);
    }

    /// <summary>从原始 JSON 文本加载并校验。</summary>
    public ContentLoadResult Load(ContentJsonSources sources)
    {
        var v = new ContentValidationResult();

        var factions = IndexById(ParseArray<FactionDef>(sources.Factions, "factions", v), f => f.Id, "faction", v);
        var generals = IndexById(ParseArray<GeneralTemplate>(sources.Generals, "generals", v), g => g.Id, "general", v);
        var units = IndexById(ParseArray<UnitTemplate>(sources.Units, "units", v), u => u.Id, "unit", v);
        var equipment = IndexById(ParseArray<EquipmentTemplate>(sources.Equipment, "equipment", v), e => e.Id, "equipment", v);
        var buildings = IndexById(ParseArray<BuildingTemplate>(sources.Buildings, "buildings", v), b => b.Id, "building", v);
        var techs = IndexById(ParseArray<TechTemplate>(sources.Techs, "tech", v), t => t.Id, "tech", v);

        var maps = new Dictionary<string, MapDef>();
        foreach (var mapJson in sources.Maps)
        {
            var map = ParseObject<MapDef>(mapJson, "map", v);
            if (map is null) continue;
            if (string.IsNullOrWhiteSpace(map.Id)) { v.Error("地图缺少 id"); continue; }
            if (!maps.TryAdd(map.Id, map)) v.Error($"地图 id 重复: {map.Id}");
        }

        var db = new ContentDatabase(factions, generals, units, equipment, buildings, techs, maps);

        Validate(db, v);

        return new ContentLoadResult { Database = db, Validation = v };
    }

    private static string? ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static List<T> ParseArray<T>(string? json, string label, ContentValidationResult v)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            v.Warning($"内容文件缺失或为空: {label}");
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            v.Error($"解析 {label} 失败: {ex.Message}");
            return new List<T>();
        }
    }

    private static T? ParseObject<T>(string json, string label, ContentValidationResult v) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            v.Error($"解析 {label} 失败: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, T> IndexById<T>(
        IEnumerable<T> items, Func<T, string> idSelector, string label, ContentValidationResult v)
    {
        var dict = new Dictionary<string, T>();
        foreach (var item in items)
        {
            var id = idSelector(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                v.Error($"{label} 存在缺少 id 的条目");
                continue;
            }

            if (!dict.TryAdd(id, item))
            {
                v.Error($"{label} id 重复: {id}");
            }
        }

        return dict;
    }

    private static void Validate(ContentDatabase db, ContentValidationResult v)
    {
        // 武将引用的默认装备
        foreach (var g in db.Generals.Values)
        {
            if (g.DefaultEquipmentId is null) continue;
            if (!db.Equipment.TryGetValue(g.DefaultEquipmentId, out var eq))
                v.Error($"武将 {g.Id} 的默认装备不存在: {g.DefaultEquipmentId}");
            else if (!eq.IsUnique)
                v.Warning($"武将 {g.Id} 自带装备 {eq.Id} 非唯一装备（设计上自带装备应为唯一）");
        }

        // 装备所需科技
        foreach (var e in db.Equipment.Values)
        {
            if (e.RequiredTechId is not null && !db.Techs.ContainsKey(e.RequiredTechId))
                v.Error($"装备 {e.Id} 所需科技不存在: {e.RequiredTechId}");
            if (e.IsUnique && !e.ForGeneralOnly)
                v.Warning($"唯一装备 {e.Id} 未限定为仅武将可用（forGeneralOnly=false）");
        }

        // 特殊兵种上限
        foreach (var u in db.Units.Values)
        {
            if (u.IsSpecial && (u.MaxCount is null || u.MaxCount <= 0))
                v.Error($"特殊兵种 {u.Id} 需要正的 maxCount");
        }

        // 科技前置与解锁引用
        foreach (var t in db.Techs.Values)
        {
            foreach (var pre in t.PrereqIds)
                if (!db.Techs.ContainsKey(pre))
                    v.Error($"科技 {t.Id} 的前置不存在: {pre}");
            foreach (var id in t.Unlocks.EquipmentIds)
                if (!db.Equipment.ContainsKey(id)) v.Error($"科技 {t.Id} 解锁的装备不存在: {id}");
            foreach (var id in t.Unlocks.UnitIds)
                if (!db.Units.ContainsKey(id)) v.Error($"科技 {t.Id} 解锁的兵种不存在: {id}");
            foreach (var id in t.Unlocks.BuildingIds)
                if (!db.Buildings.ContainsKey(id)) v.Error($"科技 {t.Id} 解锁的建筑不存在: {id}");
        }

        // 势力引用
        foreach (var f in db.Factions.Values)
        {
            foreach (var gid in f.GeneralIds)
                if (!db.Generals.ContainsKey(gid)) v.Error($"势力 {f.Id} 引用的武将不存在: {gid}");
            foreach (var uid in f.RecruitableUnitIds)
                if (!db.Units.ContainsKey(uid)) v.Error($"势力 {f.Id} 可招兵种不存在: {uid}");
        }

        foreach (var map in db.Maps.Values)
            ValidateMap(map, db, v);
    }

    private static void ValidateMap(MapDef map, ContentDatabase db, ContentValidationResult v)
    {
        var nodeIds = new HashSet<string>();
        foreach (var n in map.Nodes)
        {
            if (!nodeIds.Add(n.Id))
                v.Error($"地图 {map.Id} 节点 id 重复: {n.Id}");

            if (!string.IsNullOrEmpty(n.OwnerFactionId) && !db.Factions.ContainsKey(n.OwnerFactionId))
                v.Error($"地图 {map.Id} 节点 {n.Id} 的归属势力不存在: {n.OwnerFactionId}");

            foreach (var pb in n.Buildings)
                if (!db.Buildings.ContainsKey(pb.TemplateId))
                    v.Error($"地图 {map.Id} 节点 {n.Id} 的建筑不存在: {pb.TemplateId}");

            foreach (var gid in n.Garrison.GeneralIds)
                if (!db.Generals.ContainsKey(gid))
                    v.Error($"地图 {map.Id} 节点 {n.Id} 驻军武将不存在: {gid}");

            foreach (var stack in n.Garrison.Units)
            {
                if (!db.Units.ContainsKey(stack.TemplateId))
                    v.Error($"地图 {map.Id} 节点 {n.Id} 驻军兵种不存在: {stack.TemplateId}");
                if (stack.Count <= 0)
                    v.Warning($"地图 {map.Id} 节点 {n.Id} 驻军 {stack.TemplateId} 数量<=0");
            }
        }

        // 道路两端存在
        var adjacency = new Dictionary<string, List<string>>();
        foreach (var road in map.Roads)
        {
            if (road.Length != 2)
            {
                v.Error($"地图 {map.Id} 存在格式错误的道路（应为两个端点）");
                continue;
            }

            var (a, b) = (road[0], road[1]);
            if (!nodeIds.Contains(a)) v.Error($"地图 {map.Id} 道路端点不存在: {a}");
            if (!nodeIds.Contains(b)) v.Error($"地图 {map.Id} 道路端点不存在: {b}");
            if (nodeIds.Contains(a) && nodeIds.Contains(b))
            {
                AddEdge(adjacency, a, b);
                AddEdge(adjacency, b, a);
            }
        }

        // 出生点存在；连通性（玩家可达 AI）
        foreach (var (factionId, nodeId) in map.Spawns)
        {
            if (!nodeIds.Contains(nodeId))
                v.Error($"地图 {map.Id} 出生点节点不存在: {factionId} -> {nodeId}");
        }

        if (map.Spawns.TryGetValue("player", out var playerSpawn)
            && map.Spawns.TryGetValue("ai", out var aiSpawn)
            && nodeIds.Contains(playerSpawn) && nodeIds.Contains(aiSpawn))
        {
            if (!IsReachable(adjacency, playerSpawn, aiSpawn))
                v.Error($"地图 {map.Id} 玩家出生点 {playerSpawn} 无法通过道路到达 AI 出生点 {aiSpawn}");
        }
    }

    private static void AddEdge(Dictionary<string, List<string>> adjacency, string from, string to)
    {
        if (!adjacency.TryGetValue(from, out var list))
        {
            list = new List<string>();
            adjacency[from] = list;
        }

        if (!list.Contains(to)) list.Add(to);
    }

    private static bool IsReachable(Dictionary<string, List<string>> adjacency, string start, string goal)
    {
        var visited = new HashSet<string> { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) return true;
            if (!adjacency.TryGetValue(cur, out var neighbors)) continue;
            foreach (var n in neighbors)
                if (visited.Add(n)) queue.Enqueue(n);
        }

        return false;
    }
}
