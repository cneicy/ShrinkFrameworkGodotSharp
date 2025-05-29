using CommonSDK.Logger;
using Godot;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8603 // 可能返回 null 引用。

namespace CommonSDK.Pool;

public partial class PoolManager : Singleton<PoolManager>
{
    private readonly LogHelper _logger = new("Pool");
    private readonly Dictionary<string, Queue<Node>> _pools = new();
    private readonly Dictionary<string, PackedScene> _prefabs = new();
    
    public void InitializePool(string poolKey, PackedScene prefab, int initialSize)
    {
        if (_pools.ContainsKey(poolKey))
        {
            _logger.LogWarn($"Pool {poolKey} 已存在，跳过初始化");
            return;
        }

        var queue = new Queue<Node>();
        for (var i = 0; i < initialSize; i++)
        {
            var obj = prefab.Instantiate<Node>();
            obj.ProcessMode = ProcessModeEnum.Disabled;
            queue.Enqueue(obj);
        }

        _pools.Add(poolKey, queue);
        _prefabs.Add(poolKey, prefab);
        _logger.LogInfo($"Pool {poolKey} 初始化完成，大小: {initialSize}");
    }
    
    public Node Spawn(string poolKey, Node? parent = null)
    {
        if (!_pools.TryGetValue(poolKey, out var queue))
        {
            _logger.LogError($"对象池 {poolKey} 未初始化");
            return null;
        }

        var obj = queue.Count > 0 ? queue.Dequeue() : CreateNewObject(poolKey);

        if (obj == null)
        {
            _logger.LogError($"从对象池 {poolKey} 获取对象失败");
            return null;
        }

        obj.ProcessMode = ProcessModeEnum.Inherit;

        parent?.AddChild(obj);

        return obj;
    }
    
    public void Despawn(string poolKey, Node obj)
    {
        if (!_pools.ContainsKey(poolKey))
        {
            _logger.LogError($"对象池 {poolKey} 不存在，无法回收");
            return;
        }

        obj.ProcessMode = Node.ProcessModeEnum.Disabled;
        obj.GetParent()?.RemoveChild(obj);
        _pools[poolKey].Enqueue(obj);
    }

    private Node CreateNewObject(string poolKey)
    {
        if (_prefabs.TryGetValue(poolKey, out var prefab))
        {
            var obj = prefab.Instantiate<Node>();
            obj.ProcessMode = Node.ProcessModeEnum.Disabled;
            return obj;
        }

        _logger.LogError($"对象池 {poolKey} 无对应预制体");
        return null;
    }

    protected override void OnSingletonReady()
    {
        _logger.LogInfo("PoolManager 单例初始化完成");
    }
}
