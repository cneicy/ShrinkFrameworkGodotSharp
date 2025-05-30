namespace CommonSDK.BehaviorTree;
/// <summary>
/// 序列节点 - 依次执行子节点，直到有一个失败
/// </summary>
public partial class SequenceNode : CompositeNode
{
    private int currentChildIndex = 0;
        
    public override NodeStatus Execute(double delta)
    {
        DebugLog($"执行序列，当前子节点索引: {currentChildIndex}");
            
        for (int i = currentChildIndex; i < children.Length; i++)
        {
            var status = children[i].Execute(delta);
                
            switch (status)
            {
                case NodeStatus.Running:
                    currentChildIndex = i;
                    DebugLog($"子节点 {i} 正在运行");
                    return NodeStatus.Running;
                        
                case NodeStatus.Failure:
                    currentChildIndex = 0;
                    DebugLog($"子节点 {i} 失败，序列失败");
                    return NodeStatus.Failure;
                        
                case NodeStatus.Success:
                    DebugLog($"子节点 {i} 成功，继续下一个");
                    continue;
            }
        }
            
        currentChildIndex = 0;
        DebugLog("所有子节点都成功，序列成功");
        return NodeStatus.Success;
    }
        
    public override void Reset()
    {
        currentChildIndex = 0;
        base.Reset();
    }
}