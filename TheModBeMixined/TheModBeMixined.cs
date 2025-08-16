using CommonSDK.Event;
using CommonSDK.ModGateway;

namespace TheModBeMixined;

[Cancelable] // 添加可取消特性
public class TestEvent : EventBase
{
    public string Message { get; set; } = "Test Event";
    public int Counter { get; set; }
}

public partial class TheModBeMixined : ModBase<TheModBeMixined>
{
    private int _loopTime = 5;
    private int _physicsLoopTime = 5;
    private int _eventCounter;

    public override async Task InitAsync()
    {
        Logger.LogInfo("TheModBeMixined 初始化中...");
        await base.InitAsync();
    }

    public override async Task StartAsync()
    {
        Logger.LogInfo($"Mod loaded: {ModId} v{Version}");
        await base.StartAsync();
    }

    public override void Loop(double delta)
    {
        if (_loopTime <= 0) return;
        
        var testEvent = new TestEvent 
        { 
            Message = $"Loop event #{_eventCounter}",
            Counter = _eventCounter
        };
        
        Logger.LogInfo($"触发事件: {testEvent.Message}");
        EventBus.TriggerEvent(testEvent);
        
        if (testEvent.IsCanceled)
        {
            Logger.LogInfo($"事件 {testEvent.Message} 被取消了！");
        }
        else
        {
            Logger.LogInfo($"事件 {testEvent.Message} 处理完成");
        }
        
        _loopTime--;
        _eventCounter++;
    }

    public override void PhysicsLoop(double delta)
    {
        if (_physicsLoopTime <= 0) return;
        Logger.LogInfo("PhysicsLoop");
        _physicsLoopTime--;
    }
}