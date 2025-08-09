using CommonSDK.Event;
using Godot;
using TheModBeMixined;

namespace TheMixinMod;

[EventBusSubscriber]
public partial class EventSub : Node
{
    [EventSubscribe]
    public void TestEventSubscribe(TestEvent testEvent)
    {
        TheMixinMod.Logger.LogInfo("EventChain: BeMixin -> TheMixin");
    }
}