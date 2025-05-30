using CommonSDK.Logger;

namespace CommonSDK.BehaviorTree;

/// <summary>
/// 反转器节点 - 反转子节点的成功/失败状态
/// </summary>
public partial class InverterNode : DecoratorNode
{
    public override NodeStatus Execute(double delta)
    {
        if (child == null)
        {
            DebugLog("反转器没有子节点", LogType.Error);
            return NodeStatus.Failure;
        }
            
        var status = child.Execute(delta);
        switch (status)
        {
            case NodeStatus.Success:
                DebugLog("反转器：子节点成功 -> 失败");
                return NodeStatus.Failure;
            case NodeStatus.Failure:
                DebugLog("反转器：子节点失败 -> 成功");
                return NodeStatus.Success;
            default:
                return status;
        }
    }
}