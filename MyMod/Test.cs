using CommonSDK;
using Godot;

namespace MyMod;

public partial class Test : Singleton<Test>
{
    public void Start()
    {
        GD.Print(GetPath());
    }
}