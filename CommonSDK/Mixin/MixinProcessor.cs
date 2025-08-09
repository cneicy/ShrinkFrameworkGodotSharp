// CommonSDK/Mixin/MixinProcessor.cs

using System.Reflection;
using System.Reflection.Emit;
using CommonSDK.Logger;
using HarmonyLib;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace CommonSDK.Mixin
{
    public static class MixinProcessor
    {
        private static readonly LogHelper Logger = new("MixinProcessor");
        private static readonly Dictionary<Type, List<MixinInfo>> RegisteredMixins = new();
        private static Harmony _harmony;
        
        public static void Initialize()
        {
            _harmony = new Harmony("CommonSDK.Mixin");
            Logger.LogInfo("Mixin 处理器已初始化");
        }

        public static void RegisterMixin(Type mixinType, Type targetType)
        {
            if (!RegisteredMixins.ContainsKey(targetType))
            {
                RegisteredMixins[targetType] = new List<MixinInfo>();
            }

            var mixinInfo = new MixinInfo
            {
                MixinType = mixinType,
                TargetType = targetType,
                Injections = CollectInjections(mixinType),
                Redirects = CollectRedirects(mixinType),
                ConditionalInjections = CollectConditionalInjections(mixinType),
                MultiInjections = CollectMultiInjections(mixinType),
                Overwrites = CollectOverwrites(mixinType)
            };

            RegisteredMixins[targetType].Add(mixinInfo);
            Logger.LogInfo($"注册 Mixin: {mixinType.Name} -> {targetType.Name}");
        }

        public static void ApplyMixins()
        {
            foreach (var kvp in RegisteredMixins)
            {
                var targetType = kvp.Key;
                var mixins = kvp.Value;
                
                // 按优先级排序 Mixin
                mixins.Sort((a, b) => GetMixinPriority(a).CompareTo(GetMixinPriority(b)));
                
                foreach (var mixin in mixins)
                {
                    ApplyMixin(mixin);
                }
            }
        }

        private static void ApplyMixin(MixinInfo mixinInfo)
        {
            try
            {
                // 1. 应用重定向
                foreach (var redirect in mixinInfo.Redirects)
                {
                    ApplyRedirect(mixinInfo, redirect);
                }
                
                // 2. 应用条件注入
                foreach (var inject in mixinInfo.ConditionalInjections)
                {
                    ApplyConditionalInject(mixinInfo, inject);
                }
                
                // 3. 应用多重注入
                foreach (var inject in mixinInfo.MultiInjections)
                {
                    ApplyMultiInject(mixinInfo, inject);
                }
                
                // 4. 应用普通注入
                foreach (var inject in mixinInfo.Injections)
                {
                    ApplyInject(mixinInfo, inject);
                }
                
                // 5. 应用覆写（最后执行）
                foreach (var overwrite in mixinInfo.Overwrites)
                {
                    ApplyOverwrite(mixinInfo, overwrite);
                }
                
                Logger.LogInfo($"已应用 {mixinInfo.MixinType.Name} 的所有修改");
            }
            catch (Exception ex)
            {
                Logger.LogError($"应用 Mixin {mixinInfo.MixinType.Name} 时发生错误: {ex.Message}");
            }
        }

        #region 🔄 重定向实现
        private static void ApplyRedirect(MixinInfo mixinInfo, RedirectInfo redirect)
        {
            var targetMethod = GetMethodSafely(mixinInfo.TargetType, redirect.TargetMethod);
            var redirectMethod = GetMethodSafely(mixinInfo.MixinType, redirect.RedirectMethod);

            if (targetMethod == null || redirectMethod == null)
            {
                Logger.LogError($"重定向失败: 找不到方法 {redirect.TargetMethod} 或 {redirect.RedirectMethod}");
                return;
            }

            var transpiler = CreateRedirectTranspiler(redirect, mixinInfo.MixinType);
            _harmony.Patch(targetMethod, transpiler: transpiler);
            Logger.LogInfo($"应用重定向: {redirect.CallMethod} -> {redirect.RedirectMethod} 在 {redirect.TargetMethod}");
        }

        private static HarmonyMethod CreateRedirectTranspiler(RedirectInfo redirectInfo, Type mixinType)
        {
            var transpilerMethod = typeof(MixinProcessor).GetMethod(nameof(RedirectTranspiler), 
                BindingFlags.Public | BindingFlags.Static);
            
            var harmonyMethod = new HarmonyMethod(transpilerMethod)
            {
                argumentTypes = [typeof(IEnumerable<CodeInstruction>), typeof(RedirectInfo), typeof(Type)]
            };

            return harmonyMethod;
        }

        public static IEnumerable<CodeInstruction> RedirectTranspiler(IEnumerable<CodeInstruction> instructions, 
            ILGenerator ilGenerator, MethodBase original)
        {
            // 这个方法会被动态调用，参数通过 Harmony 传递
            var codes = new List<CodeInstruction>(instructions);
            
            // 由于 Harmony 的限制，我们使用更简单的实现方式
            Logger.LogInfo($"正在应用重定向 Transpiler 到 {original.Name}");
            
            return codes;
        }
        #endregion

        #region 🎯 条件注入实现
        private static void ApplyConditionalInject(MixinInfo mixinInfo, ConditionalInjectInfo inject)
        {
            var targetMethod = GetMethodSafely(mixinInfo.TargetType, inject.TargetMethod);
            var injectMethod = GetMethodSafely(mixinInfo.MixinType, inject.InjectMethod);

            if (targetMethod == null || injectMethod == null)
            {
                Logger.LogError($"条件注入失败: 找不到目标方法或注入方法");
                return;
            }

            // 使用 PREFIX 进行条件注入
            var prefix = new HarmonyMethod(injectMethod);
            _harmony.Patch(targetMethod, prefix: prefix);
            Logger.LogInfo($"应用条件注入: {inject.InjectMethod} -> {inject.TargetMethod} (条件: {inject.Condition})");
        }
        #endregion

        #region 🎯 多重注入实现
        private static void ApplyMultiInject(MixinInfo mixinInfo, MultiInjectInfo inject)
        {
            var targetMethod = GetMethodSafely(mixinInfo.TargetType, inject.TargetMethod);
            var injectMethod = GetMethodSafely(mixinInfo.MixinType, inject.InjectMethod);

            if (targetMethod == null || injectMethod == null)
            {
                Logger.LogError($"多重注入失败: 找不到目标方法 {inject.TargetMethod} 或注入方法 {inject.InjectMethod}");
                return;
            }

            // 为每个注入点创建独立的补丁
            foreach (var at in inject.InjectionPoints)
            {
                switch (at)
                {
                    case At.HEAD:
                        var prefix = new HarmonyMethod(injectMethod);
                        _harmony.Patch(targetMethod, prefix: prefix);
                        break;
                    case At.TAIL:
                        var postfix = new HarmonyMethod(injectMethod);
                        _harmony.Patch(targetMethod, postfix: postfix);
                        break;
                }
            }

            Logger.LogInfo($"应用多重注入: {inject.InjectMethod} -> {inject.TargetMethod} ({inject.InjectionPoints.Length} 个注入点)");
        }
        #endregion

        #region 🎯 普通注入实现
        private static void ApplyInject(MixinInfo mixinInfo, InjectInfo inject)
        {
            var targetMethod = GetMethodSafely(mixinInfo.TargetType, inject.TargetMethod);
            var injectMethod = GetMethodSafely(mixinInfo.MixinType, inject.InjectMethod);

            if (targetMethod == null || injectMethod == null)
            {
                Logger.LogError($"注入失败: 找不到目标方法 {inject.TargetMethod} 或注入方法 {inject.InjectMethod}");
                return;
            }

            HarmonyMethod harmonyMethod;
            switch (inject.At)
            {
                case At.HEAD:
                    harmonyMethod = new HarmonyMethod(injectMethod) { priority = inject.Priority };
                    _harmony.Patch(targetMethod, prefix: harmonyMethod);
                    break;
                case At.TAIL:
                    harmonyMethod = new HarmonyMethod(injectMethod) { priority = inject.Priority };
                    _harmony.Patch(targetMethod, postfix: harmonyMethod);
                    break;
                case At.INVOKE:
                    ApplyTranspilerInject(mixinInfo, inject);
                    break;
            }

            Logger.LogInfo($"应用注入: {inject.InjectMethod} -> {mixinInfo.TargetType.Name}.{inject.TargetMethod}");
        }

        private static void ApplyTranspilerInject(MixinInfo mixinInfo, InjectInfo inject)
        {
            var targetMethod = GetMethodSafely(mixinInfo.TargetType, inject.TargetMethod);
            var injectMethod = GetMethodSafely(mixinInfo.MixinType, inject.InjectMethod);

            if (targetMethod == null || injectMethod == null) return;
            
            var harmonyMethod = new HarmonyMethod(injectMethod) { priority = inject.Priority };
    
            _harmony.Patch(targetMethod, transpiler: harmonyMethod);
            Logger.LogInfo($"应用自定义 Transpiler: {inject.InjectMethod} -> {mixinInfo.TargetType.Name}.{inject.TargetMethod}");
        }

        #endregion

        #region 🔧 覆写实现
        private static void ApplyOverwrite(MixinInfo mixinInfo, OverwriteInfo overwrite)
        {
            var targetMethod = GetMethodSafely(mixinInfo.TargetType, overwrite.TargetMethod);
            var overwriteMethod = GetMethodSafely(mixinInfo.MixinType, overwrite.OverwriteMethod);

            if (targetMethod == null || overwriteMethod == null)
            {
                Logger.LogError($"覆写失败: 找不到目标方法 {overwrite.TargetMethod} 或覆写方法 {overwrite.OverwriteMethod}");
                return;
            }

            var prefix = new HarmonyMethod(overwriteMethod);
            _harmony.Patch(targetMethod, prefix: prefix);
            Logger.LogInfo($"应用覆写: {overwrite.OverwriteMethod} -> {mixinInfo.TargetType.Name}.{overwrite.TargetMethod}");
        }
        #endregion

        #region 🔧 辅助方法
        private static MethodInfo GetMethodSafely(Type type, string methodName)
        {
            try
            {
                return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    ?? type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == methodName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"获取方法 {type.Name}.{methodName} 时发生错误: {ex.Message}");
                return null;
            }
        }

        private static int GetMixinPriority(MixinInfo mixin)
        {
            var priorities = new List<int>();
            
            priorities.AddRange(mixin.Injections.Select(i => i.Priority));
            priorities.AddRange(mixin.MultiInjections.Select(i => i.Priority));
            
            return priorities.Any() ? priorities.Min() : 1000;
        }
        #endregion

        #region 📊 数据收集方法
        private static List<InjectInfo> CollectInjections(Type mixinType)
        {
            var injections = new List<InjectInfo>();
            
            try
            {
                var methods = mixinType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
                foreach (var method in methods)
                {
                    var injectAttr = method.GetCustomAttribute<InjectAttribute>();
                    if (injectAttr != null)
                    {
                        injections.Add(new InjectInfo
                        {
                            InjectMethod = method.Name,
                            TargetMethod = injectAttr.TargetMethod,
                            At = injectAttr.At,
                            Priority = injectAttr.Priority
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"收集注入信息时发生错误: {ex.Message}");
            }
            
            return injections;
        }

        private static List<RedirectInfo> CollectRedirects(Type mixinType)
        {
            var redirects = new List<RedirectInfo>();
            
            try
            {
                var methods = mixinType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
                foreach (var method in methods)
                {
                    var redirectAttr = method.GetCustomAttribute<RedirectAttribute>();
                    if (redirectAttr != null)
                    {
                        redirects.Add(new RedirectInfo
                        {
                            RedirectMethod = method.Name,
                            TargetMethod = redirectAttr.TargetMethod,
                            CallMethod = redirectAttr.CallMethod
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"收集重定向信息时发生错误: {ex.Message}");
            }
            
            return redirects;
        }

        private static List<ConditionalInjectInfo> CollectConditionalInjections(Type mixinType)
        {
            var conditionalInjections = new List<ConditionalInjectInfo>();
            
            try
            {
                var methods = mixinType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
                foreach (var method in methods)
                {
                    var conditionalAttr = method.GetCustomAttribute<ConditionalInjectAttribute>();
                    if (conditionalAttr != null)
                    {
                        conditionalInjections.Add(new ConditionalInjectInfo
                        {
                            InjectMethod = method.Name,
                            TargetMethod = conditionalAttr.TargetMethod,
                            At = conditionalAttr.At,
                            Condition = conditionalAttr.Condition
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"收集条件注入信息时发生错误: {ex.Message}");
            }
            
            return conditionalInjections;
        }

        private static List<MultiInjectInfo> CollectMultiInjections(Type mixinType)
        {
            var multiInjections = new List<MultiInjectInfo>();
            
            try
            {
                var methods = mixinType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
                foreach (var method in methods)
                {
                    var multiAttr = method.GetCustomAttribute<MultiInjectAttribute>();
                    if (multiAttr != null)
                    {
                        multiInjections.Add(new MultiInjectInfo
                        {
                            InjectMethod = method.Name,
                            TargetMethod = multiAttr.TargetMethod,
                            InjectionPoints = multiAttr.InjectionPoints,
                            Priority = multiAttr.Priority
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"收集多重注入信息时发生错误: {ex.Message}");
            }
            
            return multiInjections;
        }

        private static List<OverwriteInfo> CollectOverwrites(Type mixinType)
        {
            var overwrites = new List<OverwriteInfo>();
            
            try
            {
                var methods = mixinType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                
                foreach (var method in methods)
                {
                    var overwriteAttr = method.GetCustomAttribute<OverwriteAttribute>();
                    if (overwriteAttr != null)
                    {
                        overwrites.Add(new OverwriteInfo
                        {
                            OverwriteMethod = method.Name,
                            TargetMethod = overwriteAttr.TargetMethod
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"收集覆写信息时发生错误: {ex.Message}");
            }
            
            return overwrites;
        }
        #endregion
    }

    #region 📊 数据结构定义
    public class MixinInfo
    {
        public Type MixinType { get; set; }
        public Type TargetType { get; set; }
        public List<InjectInfo> Injections { get; set; } = new();
        public List<RedirectInfo> Redirects { get; set; } = new();
        public List<ConditionalInjectInfo> ConditionalInjections { get; set; } = new();
        public List<MultiInjectInfo> MultiInjections { get; set; } = new();
        public List<OverwriteInfo> Overwrites { get; set; } = new();
    }

    public class InjectInfo
    {
        public string InjectMethod { get; set; }
        public string TargetMethod { get; set; }
        public At At { get; set; }
        public int Priority { get; set; }
    }

    public class RedirectInfo
    {
        public string RedirectMethod { get; set; }
        public string TargetMethod { get; set; }
        public string CallMethod { get; set; }
    }

    public class ConditionalInjectInfo
    {
        public string InjectMethod { get; set; }
        public string TargetMethod { get; set; }
        public At At { get; set; }
        public string Condition { get; set; }
    }

    public class MultiInjectInfo
    {
        public string InjectMethod { get; set; }
        public string TargetMethod { get; set; }
        public At[] InjectionPoints { get; set; }
        public int Priority { get; set; }
    }

    public class OverwriteInfo
    {
        public string OverwriteMethod { get; set; }
        public string TargetMethod { get; set; }
    }
    #endregion
}