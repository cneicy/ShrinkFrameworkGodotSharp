namespace CommonSDK.BehaviorTree;

// ========== 装饰器节点 ==========
    
/// <summary>
/// 装饰器节点基类
/// </summary>
public abstract partial class DecoratorNode : BehaviorNode
{
    protected BehaviorNode child;
        
    public override void _Ready()
    {
        var children = GetChildren().OfType<BehaviorNode>().ToArray();
        if (children.Length > 0)
        {
            child = children[0];
        }
    }
        
    public override void Initialize()
    {
        if (child != null)
        {
            child.Blackboard = Blackboard;
            child.BehaviorTree = BehaviorTree;
            child.DebugMode = DebugMode;
            child.Initialize();
        }
    }
        
    public override void Reset()
    {
        child?.Reset();
    }
}