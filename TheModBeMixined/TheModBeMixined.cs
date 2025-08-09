using CommonSDK.Event;
using CommonSDK.ModGateway;

namespace TheModBeMixined;

public class TestEvent : EventBase;

public partial class TheModBeMixined : ModBase<TheModBeMixined>
{
    private int _loopTime = 5;
    private int _physicsLoopTime = 5;
    private readonly TestEvent _testEvent = new();
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
        EventBus.TriggerEvent(_testEvent);
        _loopTime--;
    }

    public override void PhysicsLoop(double delta)
    {
        if (_physicsLoopTime <= 0) return;
        Logger.LogInfo("PhysicsLoop");
        _physicsLoopTime--;
    }
}