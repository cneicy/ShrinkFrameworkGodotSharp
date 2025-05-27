using CommonSDK.Logger;
using CommonSDK.ModGateway;
using Godot;

namespace MyMod;

public partial class MyModInit : ModBase<MyModInit>, IMod
{
    public int LoopTime = 5;

    public MyModInit()
    {
        ModId = "MyMod";
    }
    public void Init()
    {
        Logger.Log(LogType.Info,"11514");
        Logger.LogInfo(Instance.GetType().FullName);
    }

    public void Loop()
    {
        if (LoopTime <= 0) return;
        Logger.LogInfo("Loop");
        LoopTime--;
    }

    public void Start()
    {
        Logger.LogInfo("Start");
    }
}