using CommonSDK.Logger;
using Godot;

namespace CommonSDK.BehaviorTree;

/// <summary>
/// 重复器节点 - 重复执行子节点
/// </summary>
public partial class RepeaterNode : DecoratorNode
{
    [Export] public int RepeatCount = -1; // -1表示无限重复
    [Export] public bool ResetOnFailure = true; // 失败时是否重置计数
        
    private int currentCount = 0;
        
    public override NodeStatus Execute(double delta)
    {
        if (child == null)
        {
            DebugLog("重复器没有子节点", LogType.Error);
            return NodeStatus.Failure;
        }
            
        var status = child.Execute(delta);
            
        if (status != NodeStatus.Running)
        {
            if (status == NodeStatus.Success || ResetOnFailure)
            {
                currentCount++;
                child.Reset();
                    
                DebugLog($"重复器：完成第 {currentCount} 次执行");
                    
                if (RepeatCount > 0 && currentCount >= RepeatCount)
                {
                    currentCount = 0;
                    DebugLog("重复器：达到重复次数，执行完成");
                    return NodeStatus.Success;
                }
            }
        }
            
        return RepeatCount == -1 ? NodeStatus.Running : status;
    }
        
    public override void Reset()
    {
        currentCount = 0;
        base.Reset();
    }
}