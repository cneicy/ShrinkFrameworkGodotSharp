using CommonSDK.Logger;
using Godot;

namespace CommonSDK.StateMachine;

/// <summary>
/// 状态基类 - 所有状态都需要继承此类
/// </summary>
public abstract partial class State : Node
{
    /// <summary>
    /// 状态机引用
    /// </summary>
    public StateMachine StateMachine { get; set; }
        
    /// <summary>
    /// 状态数据字典，用于在状态间传递数据
    /// </summary>
    protected Dictionary<string, object> StateData => StateMachine?.StateData;
        
    /// <summary>
    /// 日志帮助类
    /// </summary>
    protected LogHelper Logger => StateMachine?.Logger;
        
    /// <summary>
    /// 进入状态时调用
    /// </summary>
    public virtual void Enter() { }
        
    /// <summary>
    /// 退出状态时调用
    /// </summary>
    public virtual void Exit() { }
        
    /// <summary>
    /// 每帧更新 (在_Process中调用)
    /// </summary>
    /// <param name="delta">帧时间间隔</param>
    public virtual void Update(double delta) { }
        
    /// <summary>
    /// 物理更新 (在_PhysicsProcess中调用)
    /// </summary>
    /// <param name="delta">物理帧时间间隔</param>
    public virtual void PhysicsUpdate(double delta) { }
        
    /// <summary>
    /// 处理输入事件
    /// </summary>
    /// <param name="event">输入事件</param>
    public virtual void HandleInput(InputEvent @event) { }
        
    /// <summary>
    /// 获取状态数据
    /// </summary>
    protected T GetStateData<T>(string key, T defaultValue = default(T))
    {
        if (StateData != null && StateData.ContainsKey(key))
        {
            return (T)StateData[key];
        }
        return defaultValue;
    }
        
    /// <summary>
    /// 设置状态数据
    /// </summary>
    protected void SetStateData<T>(string key, T value)
    {
        if (StateData != null)
        {
            StateData[key] = value;
        }
    }
        
    /// <summary>
    /// 输出调试日志
    /// </summary>
    protected void DebugLog(string message, LogType logType = LogType.Info)
    {
        if (StateMachine?.DebugMode == true)
        {
            Logger?.Log(logType, $"[State-{Name}] {message}");
        }
    }
}