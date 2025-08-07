using CommonSDK.Event;
using CommonSDK.Utils;
using Godot;

namespace ShrinkFrameworkGodotSharp;
public partial class Game : Node
{
    public override async void _Ready()
    {
        /*// 创建事件处理器实例并添加到场景树
        var eventHandler = new GameEventHandler();
        AddChild(eventHandler);
        
        // 使用AutoRegister方法自动注册事件处理程序
        EventBus.Instance.AutoRegister(eventHandler);
        
        
        // 创建并触发事件
        var healthEvent = new PlayerHealthChangedEvent 
        { 
            OldHealth = 100, 
            NewHealth = 75 
        };
        
        // 异步触发事件并等待完成
        await EventBus.Instance.TriggerEventAsync(healthEvent);
        
        // 再次触发事件
        healthEvent.OldHealth = 75;
        healthEvent.NewHealth = 50;
        await EventBus.Instance.TriggerEventAsync(healthEvent);
        
        // 清理
        EventBus.Instance.UnregisterAllEventsForObject(eventHandler);*/
        
        //以下是异步生效的必须手段，不能够从特性自动注册
        
        // 创建事件处理器实例并添加到场景树
        var eventHandler = new GameEventHandler();
        AddChild(eventHandler);
        
        // 确保异步方法被正确注册
        EventBus.Instance.RegisterEvent<PlayerHealthChangedEvent>(eventHandler.OnPlayerHealthChangedAsync);
        
        // 创建并触发事件
        var healthEvent = new PlayerHealthChangedEvent 
        { 
            OldHealth = 100, 
            NewHealth = 75 
        };
        
        // 使用异步触发方法
        await EventBus.Instance.TriggerEventAsync(healthEvent);
        
        // 再次触发事件
        healthEvent.OldHealth = 75;
        healthEvent.NewHealth = 50;
        await EventBus.Instance.TriggerEventAsync(healthEvent);
        
        // 清理
        EventBus.Instance.UnregisterAllEventsForObject(eventHandler);
    }
}