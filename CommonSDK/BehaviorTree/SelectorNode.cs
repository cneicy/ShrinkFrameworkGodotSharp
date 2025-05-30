namespace CommonSDK.BehaviorTree;

/// <summary>
/// 选择器节点 - 依次执行子节点，直到有一个成功
/// </summary>
public partial class SelectorNode : CompositeNode
{
    private int currentChildIndex = 0;
        
    public override NodeStatus Execute(double delta)
    {
        DebugLog($"执行选择器，当前子节点索引: {currentChildIndex}");
            
        for (int i = currentChildIndex; i < children.Length; i++)
        {
            var status = children[i].Execute(delta);
                
            switch (status)
            {
                case NodeStatus.Running:
                    currentChildIndex = i;
                    DebugLog($"子节点 {i} 正在运行");
                    return NodeStatus.Running;
                        
                case NodeStatus.Success:
                    currentChildIndex = 0;
                    DebugLog($"子节点 {i} 成功，选择器成功");
                    return NodeStatus.Success;
                        
                case NodeStatus.Failure:
                    DebugLog($"子节点 {i} 失败，尝试下一个");
                    continue;
            }
        }
            
        currentChildIndex = 0;
        DebugLog("所有子节点都失败，选择器失败");
        return NodeStatus.Failure;
    }
        
    public override void Reset()
    {
        currentChildIndex = 0;
        base.Reset();
    }
}