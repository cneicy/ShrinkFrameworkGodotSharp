using CommonSDK.ModGateway;

namespace MyMod;

public partial class MyModInit : ModBase<MyModInit>, IMod
{
    public int LoopTime = 5;
    
    public override void _Ready()
    {
        // 使用元数据初始化后的属性
        Logger.LogInfo($"Mod loaded: {ModId} v{Version}");
    }

    public void Init()
    {
        Logger.LogInfo("Initializing mod...");
    }

    public void Start()
    {
        Logger.LogInfo("Mod started");
    }

    public void Loop()
    {
        if (LoopTime <= 0) return;
        Logger.LogInfo("Loop");
        LoopTime--;
    }
}