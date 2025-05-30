// ========== Godot 4.4 行为树系统 - 单例版本 ==========

using CommonSDK.Logger;
using Godot;

namespace CommonSDK.BehaviorTree;

/// <summary>
/// 行为树节点执行状态
/// </summary>
public enum NodeStatus
{
    Running, // 正在执行
    Success, // 执行成功
    Failure // 执行失败
}

/// <summary>
/// 行为树节点基类
/// </summary>
public abstract partial class BehaviorNode : Node
{
    /// <summary>
    /// 节点名称（用于调试）
    /// </summary>
    [Export] public string NodeName = "";

    /// <summary>
    /// 是否启用调试日志
    /// </summary>
    [Export] public bool DebugMode;

    /// <summary>
    /// 黑板数据引用
    /// </summary>
    public Blackboard Blackboard { get; set; }

    /// <summary>
    /// 行为树引用
    /// </summary>
    public BehaviorTree BehaviorTree { get; set; }

    /// <summary>
    /// 执行节点逻辑
    /// </summary>
    /// <param name="delta">时间间隔</param>
    /// <returns>节点执行状态</returns>
    public abstract NodeStatus Execute(double delta);

    /// <summary>
    /// 初始化节点
    /// </summary>
    public virtual void Initialize()
    {
    }

    /// <summary>
    /// 重置节点状态
    /// </summary>
    public virtual void Reset()
    {
    }

    /// <summary>
    /// 调试日志输出
    /// </summary>
    protected void DebugLog(string message, LogType logType = LogType.Info)
    {
        if (!DebugMode || BehaviorTree?.Logger == null) return;
        var name = !string.IsNullOrEmpty(NodeName) ? NodeName : GetType().Name;
        BehaviorTree.Logger.Log(logType, $"[Node-{name}] {message}");
    }
}