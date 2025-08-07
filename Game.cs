using CommonSDK.Event;
using Godot;

namespace ShrinkFrameworkGodotSharp;

public partial class Game : Node
{
    public override async void _Ready()
    {
        var eventHandler = new GameEventHandler();
        AddChild(eventHandler);
        
        EventBus.AutoRegister(eventHandler);
        
        var healthEvent = new PlayerHealthChangedEvent 
        { 
            OldHealth = 100, 
            NewHealth = 75 
        };
        await EventBus.TriggerEventAsync(healthEvent);
        //以下同步
        healthEvent.NewHealth = 50;
        healthEvent.OldHealth = 75;
        EventBus.TriggerEvent(healthEvent);
    }
}