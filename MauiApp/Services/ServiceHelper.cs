namespace MauiApp.Services;

/// <summary>
/// 简易服务定位器：本项目页面多为手动 new（非 DI 构造），用它在页面里取单例服务（如 <see cref="AudioService"/>）。
/// 在 <c>MauiProgram</c> 构建完成后注入 <see cref="Services"/>。
/// </summary>
public static class ServiceHelper
{
    public static IServiceProvider Services { get; set; } = default!;

    public static T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public static T? GetOrNull<T>() where T : class => Services?.GetService(typeof(T)) as T;
}
