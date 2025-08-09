using CommonSDK.ModGateway;

namespace TheMixinMod;

public partial class TheMixinMod : ModBase<TheMixinMod>
{
    public override async Task InitAsync()
    {
        await base.InitAsync();
        Logger.LogInfo("TheMixinMod Initialized...");
    }

    public override async Task StartAsync()
    {
        Logger.LogInfo("TheMixinMod Starting...");

        var eventSub = new EventSub();
        await CallDeferredAddChildAsync(eventSub);
        
        Logger.LogInfo($"Mod loaded: {ModId} v{Version}");
        await base.StartAsync();
    }

    public override void Loop(double delta)
    {
        
    }

    public override void PhysicsLoop(double delta)
    {
        
    }
}