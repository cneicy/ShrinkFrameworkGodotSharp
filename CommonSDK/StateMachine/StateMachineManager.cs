using CommonSDK.Logger;

namespace CommonSDK.StateMachine;

/// <summary>
/// 状态机管理器单例 - 用于全局状态管理
/// </summary>
public partial class StateMachineManager : Singleton<StateMachineManager>
{
    private Dictionary<string, StateMachine> _stateMachines = new Dictionary<string, StateMachine>();
    private readonly LogHelper _logger = new("StateMachineManager");
    protected override void OnSingletonReady()
    {
        _logger.LogInfo("状态机管理器已初始化");
    }
        
    protected override void OnSingletonDestroy()
    {
        _stateMachines.Clear();
        _logger.LogInfo("状态机管理器已销毁");
    }
        
    /// <summary>
    /// 注册状态机
    /// </summary>
    /// <param name="name">状态机名称</param>
    /// <param name="stateMachine">状态机实例</param>
    public void RegisterStateMachine(string name, StateMachine stateMachine)
    {
        _stateMachines[name] = stateMachine;
        _logger.LogInfo($"注册状态机: {name}");
    }
        
    /// <summary>
    /// 获取状态机
    /// </summary>
    /// <param name="name">状态机名称</param>
    /// <returns>状态机实例</returns>
    public StateMachine GetStateMachine(string name)
    {
        return _stateMachines.TryGetValue(name, out StateMachine sm) ? sm : null;
    }
        
    /// <summary>
    /// 移除状态机
    /// </summary>
    /// <param name="name">状态机名称</param>
    public void UnregisterStateMachine(string name)
    {
        if (_stateMachines.Remove(name))
        {
            _logger.LogInfo($"移除状态机: {name}");
        }
    }
        
    /// <summary>
    /// 获取所有已注册的状态机名称
    /// </summary>
    /// <returns>状态机名称数组</returns>
    public string[] GetRegisteredStateMachines()
    {
        return _stateMachines.Keys.ToArray();
    }
}