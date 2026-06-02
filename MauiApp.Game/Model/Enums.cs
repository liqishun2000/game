namespace MauiApp.Game.Model;

/// <summary>势力类型。</summary>
public enum FactionKind
{
    Player,
    Ai,
    Rebel,
}

/// <summary>AI 难度。</summary>
public enum AiDifficulty
{
    Easy,
    Normal,
    Hard,
}

/// <summary>地盘类型。</summary>
public enum TileType
{
    Village,
    City,
    Pass,
}

/// <summary>武将局内状态。</summary>
public enum GeneralStatus
{
    Active,
    Captured,
    Escaped,
    Dead,
}

/// <summary>资源类型。</summary>
public enum ResourceType
{
    Gold,
    Food,
    TechPoints,
}
