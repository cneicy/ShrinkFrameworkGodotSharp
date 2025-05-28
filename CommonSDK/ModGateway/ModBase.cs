using CommonSDK.Logger;
using Godot;
// ReSharper disable StaticMemberInGenericType
// ReSharper disable MemberCanBeProtected.Global
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
#pragma warning disable CA2211

namespace CommonSDK.ModGateway;

public abstract partial class ModBase<T> : Node where T : Node, IMod, new()
{
    public string ModId { get; set; }
    public string Description { get; set; }
    public string Version { get; set; }
    public string[] Author { get; set; }
    
    public static T Instance { get; } = new();
    public LogHelper Logger => new(ModId);
}