namespace MauiApp.Game.World;

/// <summary>大地图操作结果（建造/招兵等）。</summary>
public readonly record struct OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message = "") => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
