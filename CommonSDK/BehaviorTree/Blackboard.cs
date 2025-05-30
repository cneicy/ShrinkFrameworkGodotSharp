using CommonSDK.Logger;
using Godot;

namespace CommonSDK.BehaviorTree;

/// <summary>
/// 黑板数据存储系统
/// </summary>
public partial class Blackboard : RefCounted
{
    private Dictionary<string, object> _data = new();
    private LogHelper _logger = new("Blackboard");
        
    /// <summary>
    /// 获取数据
    /// </summary>
    public T Get<T>(string key, T defaultValue = default(T))
    {
        if (_data.TryGetValue(key, out object value))
        {
            return (T)value;
        }
        return defaultValue;
    }
        
    /// <summary>
    /// 设置数据
    /// </summary>
    public void Set<T>(string key, T value)
    {
        _data[key] = value;
        _logger.LogInfo($"设置黑板数据: {key} = {value}");
    }
        
    /// <summary>
    /// 检查是否存在指定键
    /// </summary>
    public bool Has(string key)
    {
        return _data.ContainsKey(key);
    }
        
    /// <summary>
    /// 移除指定键
    /// </summary>
    public void Remove(string key)
    {
        if (_data.Remove(key))
        {
            _logger.LogInfo($"移除黑板数据: {key}");
        }
    }
        
    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void Clear()
    {
        _data.Clear();
        _logger.LogInfo("清空黑板数据");
    }
        
    /// <summary>
    /// 获取所有键
    /// </summary>
    public string[] GetKeys()
    {
        return _data.Keys.ToArray();
    }
}