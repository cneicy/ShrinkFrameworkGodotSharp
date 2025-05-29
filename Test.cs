using CommonSDK.Pool;
using Godot;

namespace ShrinkFrameworkGodotSharp;

public partial class Test : Node
{
    [Export] public PackedScene Prefab;
    [Export] public Node Parent;
    public override void _Ready()
    {
        base._Ready();
        PoolManager.Instance.InitializePool("test",Prefab,10);
        var obj = PoolManager.Instance.Spawn("test",Parent);
        GD.Print(obj.GetPath());
    }
}