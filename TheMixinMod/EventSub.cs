using CommonSDK.Event;
using Godot;
using TheModBeMixined;

namespace TheMixinMod;

[EventBusSubscriber]
public partial class EventSub : Node
{
    [EventSubscribe(EventPriority.HIGHEST)]
    public void OnTestEventHighest(TestEvent testEvent)
    {
        // 访问事件的详细信息
        TheMixinMod.Logger.LogInfo($"[{testEvent.Phase}] 当前处理程序: {testEvent.CurrentHandler?.MethodName}");
        TheMixinMod.Logger.LogInfo($"事件ID: {testEvent.EventId}");
        TheMixinMod.Logger.LogInfo($"事件时间: {testEvent.EventTime}");
        
        // 获取所有订阅者信息
        var subscribers = testEvent.GetSubscribers();
        TheMixinMod.Logger.LogInfo($"总订阅者数: {subscribers.Length}");
        
        foreach (var subscriber in subscribers)
        {
            TheMixinMod.Logger.LogInfo($"订阅者: {subscriber.DeclaringType.Name}.{subscriber.MethodName} (优先级: {subscriber.Priority})");
        }

        // 可以取消事件
        if (testEvent.Counter % 2 != 0) return;
        testEvent.SetCanceled(true);
        TheMixinMod.Logger.LogInfo("事件被取消");
    }

    [EventSubscribe(EventPriority.MONITOR, receiveCanceled: true)]
    public void MonitorTestEvent(TestEvent testEvent)
    {
        // 监控阶段 - 接收所有事件包括已取消的
        TheMixinMod.Logger.LogInfo($"[MONITOR] 事件完整调试信息:");
        TheMixinMod.Logger.LogInfo(testEvent.GetEventDebugInfo());
    }
}