namespace MauiApp.Rendering;

/// <summary>简单对象池，减少战斗演出中频繁分配。</summary>
public sealed class ObjectPool<T> where T : class, new()
{
    private readonly Stack<T> _stack = new();
    private readonly Action<T>? _reset;

    public ObjectPool(Action<T>? reset = null) => _reset = reset;

    public T Rent()
    {
        if (_stack.Count == 0) return new T();
        return _stack.Pop();
    }

    public void Return(T item)
    {
        _reset?.Invoke(item);
        _stack.Push(item);
    }
}
