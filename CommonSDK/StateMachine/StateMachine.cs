using CommonSDK.Logger;
using Godot;

namespace CommonSDK.StateMachine;


    
/// <summary>
/// 状态机管理器
/// </summary>
public partial class StateMachine : Node
{
    [Signal]
    public delegate void StateChangedEventHandler(string oldStateName, string newStateName);
        
    [Export] 
    public State InitialState;
        
    [Export] 
    public bool DebugMode = false;
        
    private State _currentState;
    private State _previousState;
    private Dictionary<string, State> _states = new Dictionary<string, State>();
        
    /// <summary>
    /// 状态数据字典，用于在状态间传递数据
    /// </summary>
    public Dictionary<string, object> StateData { get; private set; } = new Dictionary<string, object>();
        
    /// <summary>
    /// 日志帮助类实例
    /// </summary>
    public LogHelper Logger { get; private set; }
        
    /// <summary>
    /// 当前状态
    /// </summary>
    public State CurrentState => _currentState;
        
    /// <summary>
    /// 前一个状态
    /// </summary>
    public State PreviousState => _previousState;
        
    /// <summary>
    /// 所有状态列表
    /// </summary>
    public IReadOnlyDictionary<string, State> States => _states;
        
    public override void _Ready()
    {
        // 初始化日志帮助类
        Logger = new LogHelper($"StateMachine-{GetParent().Name}");
            
        // 获取所有子状态并建立索引
        var stateNodes = GetChildren().OfType<State>().ToArray();
            
        foreach (var state in stateNodes)
        {
            state.StateMachine = this;
            _states[state.Name] = state;
                
            if (DebugMode)
            {
                Logger.LogInfo($"注册状态: {state.Name}");
            }
        }
            
        // 切换到初始状态
        if (InitialState != null)
        {
            ChangeState(InitialState);
        }
        else if (_states.Count > 0)
        {
            ChangeState(_states.Values.First());
        }
    }
        
    public override void _Process(double delta)
    {
        _currentState?.Update(delta);
    }
        
    public override void _PhysicsProcess(double delta)
    {
        _currentState?.PhysicsUpdate(delta);
    }
        
    public override void _UnhandledInput(InputEvent @event)
    {
        _currentState?.HandleInput(@event);
    }
        
    /// <summary>
    /// 切换到指定状态
    /// </summary>
    /// <param name="newState">目标状态</param>
    public void ChangeState(State newState)
    {
        if (_currentState == newState) return;
        if (newState == null)
        {
            Logger.LogError("尝试切换到空状态");
            return;
        }
            
        string oldStateName = _currentState?.Name ?? "None";
        string newStateName = newState.Name;
            
        _currentState?.Exit();
        _previousState = _currentState;
        _currentState = newState;
        _currentState?.Enter();
            
        if (DebugMode)
        {
            Logger.LogInfo($"状态切换: {oldStateName} -> {newStateName}");
        }
            
        EmitSignal(SignalName.StateChanged, oldStateName, newStateName);
    }
        
    /// <summary>
    /// 通过名称切换状态
    /// </summary>
    /// <param name="stateName">状态名称</param>
    public void ChangeState(string stateName)
    {
        if (_states.TryGetValue(stateName, out State state))
        {
            ChangeState(state);
        }
        else
        {
            Logger.LogError($"找不到状态: {stateName}");
        }
    }
        
    /// <summary>
    /// 切换回前一个状态
    /// </summary>
    public void ChangeToPreviousState()
    {
        if (_previousState != null)
        {
            ChangeState(_previousState);
        }
        else
        {
            Logger.LogError("没有前一个状态可以切换");
        }
    }
        
    /// <summary>
    /// 检查当前是否为指定状态
    /// </summary>
    /// <param name="stateName">状态名称</param>
    /// <returns>是否为指定状态</returns>
    public bool IsCurrentState(string stateName)
    {
        return _currentState?.Name == stateName;
    }
        
    /// <summary>
    /// 检查当前是否为指定状态类型
    /// </summary>
    /// <typeparam name="T">状态类型</typeparam>
    /// <returns>是否为指定状态类型</returns>
    public bool IsCurrentState<T>() where T : State
    {
        return _currentState is T;
    }
        
    /// <summary>
    /// 获取指定名称的状态
    /// </summary>
    /// <param name="stateName">状态名称</param>
    /// <returns>状态对象，如果不存在则返回null</returns>
    public State GetState(string stateName)
    {
        return _states.TryGetValue(stateName, out State state) ? state : null;
    }
        
    /// <summary>
    /// 获取指定类型的状态
    /// </summary>
    /// <typeparam name="T">状态类型</typeparam>
    /// <returns>状态对象，如果不存在则返回null</returns>
    public T GetState<T>() where T : State
    {
        return _states.Values.OfType<T>().FirstOrDefault();
    }
        
    /// <summary>
    /// 清空所有状态数据
    /// </summary>
    public void ClearStateData()
    {
        StateData.Clear();
    }
        
    /// <summary>
    /// 获取状态数据
    /// </summary>
    public T GetStateData<T>(string key, T defaultValue = default(T))
    {
        if (StateData.ContainsKey(key))
        {
            return (T)StateData[key];
        }
        return defaultValue;
    }
        
    /// <summary>
    /// 设置状态数据
    /// </summary>
    public void SetStateData<T>(string key, T value)
    {
        StateData[key] = value;
    }
}