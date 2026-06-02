namespace MauiApp.Game.Content;

/// <summary>内容校验结果：收集错误与警告。</summary>
public sealed class ContentValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public void Error(string message) => Errors.Add(message);
    public void Warning(string message) => Warnings.Add(message);

    public override string ToString()
    {
        var lines = new List<string>
        {
            $"校验结果: {(IsValid ? "通过" : "失败")}  错误 {Errors.Count}  警告 {Warnings.Count}",
        };
        lines.AddRange(Errors.Select(e => "  [错误] " + e));
        lines.AddRange(Warnings.Select(w => "  [警告] " + w));
        return string.Join(Environment.NewLine, lines);
    }
}
