using Godot;

namespace CommonSDK.ModGateway;

public abstract partial class ModBase<T> : Node where T : Node, new()
{
    public string Name;
    public string Description;
    public string Author;
    public static T Instance { get; } = new();
}