namespace CommonSDK.BehaviorTree;

/// <summary>
/// 条件节点基类
/// </summary>
public abstract partial class ConditionNode : LeafNode
{
    /// <summary>
    /// 检查条件是否满足
    /// </summary>
    protected abstract bool CheckCondition();
        
    public override NodeStatus Execute(double delta)
    {
        bool result = CheckCondition();
        DebugLog($"条件检查结果: {result}");
        return result ? NodeStatus.Success : NodeStatus.Failure;
    }
}