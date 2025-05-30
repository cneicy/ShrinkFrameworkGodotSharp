namespace CommonSDK.BehaviorTree;

// ========== 组合节点 ==========
    
/// <summary>
/// 组合节点基类
/// </summary>
public abstract partial class CompositeNode : BehaviorNode
{
    protected BehaviorNode[] children;
        
    public override void _Ready()
    {
        children = GetChildren().OfType<BehaviorNode>().ToArray();
    }
        
    public override void Initialize()
    {
        foreach (var child in children)
        {
            child.Blackboard = Blackboard;
            child.BehaviorTree = BehaviorTree;
            child.DebugMode = DebugMode;
            child.Initialize();
        }
    }
        
    public override void Reset()
    {
        foreach (var child in children)
        {
            child.Reset();
        }
    }
}