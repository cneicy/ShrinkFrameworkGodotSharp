using System.Reflection;
using System.Reflection.Emit;
using CommonSDK.Event;
using CommonSDK.Logger;
using CommonSDK.Mixin;
using HarmonyLib;
using TheModBeMixined;

namespace TheMixinMod;

[Mixin(typeof(TheModBeMixined.TheModBeMixined))]
public static class MyModMixin
{
    [Inject("PhysicsLoop", At.INVOKE)]
    public static IEnumerable<CodeInstruction> InjectBeforeLogInfo(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var logHelperConstructor = typeof(LogHelper).GetConstructor([typeof(string)]);
        var logInfoMethod = typeof(LogHelper).GetMethod("LogInfo", [typeof(string)]);

        for (var i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].opcode != OpCodes.Ldsfld ||
                codes[i].operand?.ToString()?.Contains("Logger") != true ||
                i + 1 >= codes.Count ||
                codes[i + 1].opcode != OpCodes.Ldstr ||
                codes[i + 1].operand?.ToString() != "PhysicsLoop" ||
                i + 2 >= codes.Count ||
                codes[i + 2].opcode != OpCodes.Callvirt ||
                codes[i + 2].operand is not MethodInfo { Name: "LogInfo" }) continue;

            var mixinInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldstr, "MyModMixin"),
                new(OpCodes.Newobj, logHelperConstructor),
                new(OpCodes.Ldstr, "Mixin"),
                new(OpCodes.Callvirt, logInfoMethod)
            };
            
            if (codes[i].labels.Count > 0)
            {
                mixinInstructions[0].labels.AddRange(codes[i].labels);
                codes[i].labels.Clear();
            }
            codes.InsertRange(i, mixinInstructions);
            break;
        }


        return codes;
    }

    [Overwrite("Loop")]
    public static void ReplaceLoop(TheModBeMixined.TheModBeMixined __instance, double delta)
    {
        var loopTimeField = typeof(TheModBeMixined.TheModBeMixined).GetField("_loopTime",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var eventCounterField = typeof(TheModBeMixined.TheModBeMixined).GetField("_eventCounter",
            BindingFlags.NonPublic | BindingFlags.Instance);
            
        var loopTime = (int)loopTimeField.GetValue(__instance)!;
        var eventCounter = (int)eventCounterField.GetValue(__instance)!;
        
        if (loopTime <= 2)
        {
            return;
        }

        // 创建测试事件
        var testEvent = new TestEvent 
        { 
            Message = $"Mixin触发事件 #{eventCounter}",
            Counter = eventCounter
        };
        
        TheMixinMod.Logger.LogInfo($"[MIXIN] 准备触发事件: {testEvent.Message}");
        
        // 触发事件
        EventBus.TriggerEvent(testEvent);
        
        // 检查事件处理结果
        if (testEvent.IsCanceled)
        {
            TheMixinMod.Logger.LogInfo($"[MIXIN] 事件被取消，跳过后续处理: {testEvent.Message}");
        }
        else
        {
            TheMixinMod.Logger.LogInfo($"[MIXIN] 事件处理完成，继续后续操作: {testEvent.Message}");
            // 只有在事件未被取消时才执行某些操作
            DoSomeImportantWork();
        }
        
        loopTimeField.SetValue(__instance, loopTime - 1);
        eventCounterField.SetValue(__instance, eventCounter + 1);
    }
    private static void DoSomeImportantWork()
    {
        TheMixinMod.Logger.LogInfo("[MIXIN] 执行重要工作...");
    }
}