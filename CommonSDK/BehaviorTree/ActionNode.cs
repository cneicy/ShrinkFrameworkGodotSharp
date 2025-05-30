namespace CommonSDK.BehaviorTree;

/// <summary>
/// 动作节点基类
/// </summary>
public abstract partial class ActionNode : LeafNode
{
    /// <summary>
    /// 执行动作
    /// </summary>
    protected abstract NodeStatus PerformAction(double delta);
        
    public override NodeStatus Execute(double delta)
    {
        return PerformAction(delta);
    }
}