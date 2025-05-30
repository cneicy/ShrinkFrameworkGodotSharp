using Godot;

namespace CommonSDK.BehaviorTree;

/// <summary>
/// 并行节点 - 同时执行所有子节点
/// </summary>
public partial class ParallelNode : CompositeNode
{
    [Export] public int RequiredSuccessCount = -1; // -1表示需要全部成功
    [Export] public int RequiredFailureCount = 1;   // 失败节点数量达到此值时整体失败
        
    public override NodeStatus Execute(double delta)
    {
        int successCount = 0;
        int failureCount = 0;
        int runningCount = 0;
            
        foreach (var child in children)
        {
            var status = child.Execute(delta);
            switch (status)
            {
                case NodeStatus.Success:
                    successCount++;
                    break;
                case NodeStatus.Failure:
                    failureCount++;
                    break;
                case NodeStatus.Running:
                    runningCount++;
                    break;
            }
        }
            
        int targetSuccessCount = RequiredSuccessCount == -1 ? children.Length : RequiredSuccessCount;
            
        DebugLog($"并行执行状态 - 成功:{successCount}, 失败:{failureCount}, 运行中:{runningCount}");
            
        if (successCount >= targetSuccessCount)
        {
            DebugLog("并行节点成功");
            return NodeStatus.Success;
        }
            
        if (failureCount >= RequiredFailureCount)
        {
            DebugLog("并行节点失败");
            return NodeStatus.Failure;
        }
            
        return NodeStatus.Running;
    }
}