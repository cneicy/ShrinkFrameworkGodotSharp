using CommonSDK;
using Godot;

namespace MyMod;

public partial class MyModInit : ModBase<MyModInit>, IMod
{
    public int LoopTime = 5;

    public MyModInit()
    {
        Author = "Eicy";
    }

    public void Init()
    {
        GD.Print("Made by " + Author);
        GD.Print(Instance.GetType().FullName);
    }

    public void Loop()
    {
        if (LoopTime <= 0) return;
        GD.Print("Loop");
        LoopTime--;
    }

    public void Start()
    {
        new Test().Pop();
    }
}