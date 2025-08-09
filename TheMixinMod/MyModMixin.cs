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
        var loopTime = (int)loopTimeField.GetValue(__instance)!;
        if (loopTime <= -10)
        {
            return;
        }

        EventBus.TriggerEvent(new TestEvent());
        loopTimeField.SetValue(__instance, loopTime - 1);
    }
}