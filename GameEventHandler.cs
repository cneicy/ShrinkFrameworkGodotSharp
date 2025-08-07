namespace ShrinkFrameworkGodotSharp;

using Godot;
using System.Threading.Tasks;
using CommonSDK.Event;

// 事件类
public class PlayerHealthChangedEvent : EventBase
{
    public int OldHealth { get; set; }
    public int NewHealth { get; set; }
}
// 事件处理类
[EventBusSubscriber]
public partial class GameEventHandler : Node
{
    // 静态事件处理方法 - 会自动注册
    [EventSubscribe]
    public static void OnPlayerHealthChangedStatic(PlayerHealthChangedEvent evt)
    {
        GD.Print($"静态处理: 玩家生命值从 {evt.OldHealth} 变为 {evt.NewHealth}");
    }
    
    // 实例事件处理方法
    [EventSubscribe]
    public void OnPlayerHealthChangedInstance(PlayerHealthChangedEvent evt)
    {
        GD.Print($"实例处理: 玩家生命值变化，当前生命值: {evt.NewHealth}");
    }
    
    // 异步事件处理方法
    [EventSubscribe]
    public async Task OnPlayerHealthChangedAsync(PlayerHealthChangedEvent evt)
    {
        GD.Print("异步处理: 开始处理...");
        await Task.Delay(100);
        GD.Print($"异步处理: 玩家生命值变化处理完成");
    }
}