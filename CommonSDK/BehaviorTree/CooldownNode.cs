using Godot;

namespace CommonSDK.BehaviorTree;

/// <summary>
/// 冷却器节点 - 在指定时间内只能执行一次
/// </summary>
public partial class CooldownNode : DecoratorNode
{
    [Export] public float CooldownTime = 1.0f;
        
    private double lastExecuteTime = -1;
        
    public override NodeStatus Execute(double delta)
    {
        if (child == null) return NodeStatus.Failure;
            
        var currentTime = Time.GetUnixTimeFromSystem();
            
        if (lastExecuteTime >= 0 && currentTime - lastExecuteTime < CooldownTime)
        {
            DebugLog($"冷却中，剩余时间: {CooldownTime - (currentTime - lastExecuteTime):F1}秒");
            return NodeStatus.Failure;
        }
            
        var status = child.Execute(delta);
            
        if (status != NodeStatus.Running)
        {
            lastExecuteTime = currentTime;
            DebugLog("冷却器：开始冷却计时");
        }
            
        return status;
    }
        
    public override void Reset()
    {
        lastExecuteTime = -1f;
        base.Reset();
    }
}