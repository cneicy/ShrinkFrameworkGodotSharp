using Godot;

namespace CommonSDK.StateMachine;

/// <summary>
/// 状态机组件 - 可以挂载到任何节点上的组件式状态机
/// </summary>
public partial class StateMachineComponent : Node
{
    [Export] 
    public string InitialStateName = "";
        
    [Export] 
    public bool DebugMode;
        
    [Export]
    public bool RegisterToManager;
        
    [Export]
    public string ManagerRegistrationName = "";
        
    private StateMachine _stateMachine;
        
    /// <summary>
    /// 状态机引用
    /// </summary>
    public StateMachine StateMachine => _stateMachine;
        
    public override void _Ready()
    {
        _stateMachine = new StateMachine();
        _stateMachine.DebugMode = DebugMode;
        AddChild(_stateMachine);
            
        // 注册到管理器
        if (RegisterToManager)
        {
            string registrationName = !string.IsNullOrEmpty(ManagerRegistrationName) 
                ? ManagerRegistrationName 
                : GetParent().Name;
                
            StateMachineManager.Instance.RegisterStateMachine(registrationName, _stateMachine);
        }
            
        // 如果指定了初始状态名称，尝试设置
        if (!string.IsNullOrEmpty(InitialStateName))
        {
            CallDeferred(nameof(SetInitialState));
        }
    }
        
    public override void _ExitTree()
    {
        // 从管理器中移除
        if (RegisterToManager && !string.IsNullOrEmpty(ManagerRegistrationName))
        {
            StateMachineManager.Instance?.UnregisterStateMachine(ManagerRegistrationName);
        }
    }
        
    private void SetInitialState()
    {
        var initialState = _stateMachine.GetState(InitialStateName);
        if (initialState != null)
        {
            _stateMachine.ChangeState(initialState);
        }
    }
}