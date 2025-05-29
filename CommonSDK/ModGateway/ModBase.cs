using CommonSDK.Logger;
using Godot;
// ReSharper disable StaticMemberInGenericType
// ReSharper disable MemberCanBeProtected.Global
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
#pragma warning disable CA2211

namespace CommonSDK.ModGateway;

/// <summary>
/// 模组基类
/// <para>为所有模组提供基础功能和属性</para>
/// <para>使用单例模式确保每个模组类型只有一个实例</para>
/// </summary>
/// <typeparam name="T">模组类型，必须继承自Node并实现IMod接口</typeparam>
public abstract partial class ModBase<T> : Node where T : Node, IMod, new()
{
    /// <summary>
    /// 模组唯一标识符
    /// <para>用于在系统中唯一标识此模组</para>
    /// </summary>
    public string ModId { get; set; }
    
    /// <summary>
    /// 模组描述
    /// <para>简要说明模组的功能和用途</para>
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// 模组版本号
    /// <para>遵循语义化版本规范</para>
    /// </summary>
    public string Version { get; set; }
    
    /// <summary>
    /// 模组作者
    /// <para>可以包含多个作者</para>
    /// </summary>
    public string[] Author { get; set; }
    
    /// <summary>
    /// 模组实例
    /// <para>使用静态属性实现单例模式</para>
    /// <para>确保每个模组类型在整个应用程序中只有一个实例</para>
    /// </summary>
    public static T Instance { get; } = new();
    
    /// <summary>
    /// 模组日志记录器
    /// <para>提供带有模组ID前缀的日志功能</para>
    /// <para>用于记录模组特定的日志信息</para>
    /// </summary>
    public LogHelper Logger => new(ModId);
}