using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;

namespace CommonSDK.Event;

public static class EventBusGeneratedRegistration
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterStaticEventHandlers()
    {
        RegisterStaticEventHandlersWithReflection();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEventHandlers(object target)
    {
        RegisterEventHandlersWithReflection(target);
    }

    private static void RegisterStaticEventHandlersWithReflection()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // 检查类是否有 AutoRegisterEvents 特性
                    var classAttributes = type.GetCustomAttributes(typeof(EventBusSubscriberAttribute), false);
                    if (classAttributes.Length == 0) continue;

                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                    foreach (var method in methods)
                    {
                        var attributes = method.GetCustomAttributes(typeof(EventSubscribeAttribute), false);
                        if (attributes.Length == 0) continue;

                        var parameters = method.GetParameters();

                        if (parameters.Length != 1)
                        {
                            if (OS.IsDebugBuild())
                                EventBus.Logger.LogError(
                                    $"静态方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但参数数量不为1。");
                            continue;
                        }

                        var parameterType = parameters[0].ParameterType;
                        if (!typeof(EventBase).IsAssignableFrom(parameterType))
                        {
                            if (OS.IsDebugBuild())
                                EventBus.Logger.LogError(
                                    $"静态方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但参数类型 {parameterType.Name} 不继承自 EventBusEvent。");
                            continue;
                        }

                        var isAsync = method.ReturnType == typeof(Task);

                        try
                        {
                            if (isAsync)
                            {
                                var funcType = typeof(Func<,>).MakeGenericType(parameterType, typeof(Task));
                                var handlerDelegate = Delegate.CreateDelegate(funcType, method);
                                
                                // 直接调用泛型方法
                                RegisterEventGeneric(parameterType, handlerDelegate, true);
                            }
                            else if (method.ReturnType == typeof(void))
                            {
                                var actionType = typeof(Action<>).MakeGenericType(parameterType);
                                var actionDelegate = Delegate.CreateDelegate(actionType, method);

                                // 直接调用泛型方法
                                RegisterEventGeneric(parameterType, actionDelegate, false);
                            }
                            else
                            {
                                if (OS.IsDebugBuild())
                                    EventBus.Logger.LogError(
                                        $"静态方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但返回类型不是 void 或 Task。");
                                continue;
                            }

                            if (OS.IsDebugBuild())
                                EventBus.Logger.LogInfo($"自动注册静态事件: {parameterType.FullName} -> {type.Name}.{method.Name}");
                        }
                        catch (Exception ex)
                        {
                            if (OS.IsDebugBuild())
                                EventBus.Logger.LogError($"为静态事件 {parameterType.FullName} 创建委托给方法 {method.Name} 失败: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (OS.IsDebugBuild())
                    EventBus.Logger.LogError($"扫描程序集 {assembly.FullName} 时发生异常: {ex}");
            }
        }
    }

    private static void RegisterEventHandlersWithReflection(object target)
    {
        var type = target.GetType();
        
        // 检查类是否有 AutoRegisterEvents 特性
        var classAttributes = type.GetCustomAttributes(typeof(EventBusSubscriberAttribute), false);
        if (classAttributes.Length == 0) return;

        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        foreach (var method in methods)
        {
            var attributes = method.GetCustomAttributes(typeof(EventSubscribeAttribute), false);
            if (attributes.Length == 0) continue;

            // 跳过静态方法，因为它们已经在静态注册阶段处理过了
            if (method.IsStatic) continue;

            var parameters = method.GetParameters();

            if (parameters.Length != 1)
            {
                if (OS.IsDebugBuild())
                    EventBus.Logger.LogError(
                        $"方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但参数数量不为1。");
                continue;
            }

            var parameterType = parameters[0].ParameterType;
            if (!typeof(EventBase).IsAssignableFrom(parameterType))
            {
                if (OS.IsDebugBuild())
                    EventBus.Logger.LogError(
                        $"方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但参数类型 {parameterType.Name} 不继承自 EventBusEvent。");
                continue;
            }

            var isAsync = method.ReturnType == typeof(Task);

            try
            {
                if (isAsync)
                {
                    var funcType = typeof(Func<,>).MakeGenericType(parameterType, typeof(Task));
                    var handlerDelegate = Delegate.CreateDelegate(funcType, target, method);
                    
                    // 直接调用泛型方法
                    RegisterEventGeneric(parameterType, handlerDelegate, true);
                }
                else if (method.ReturnType == typeof(void))
                {
                    var actionType = typeof(Action<>).MakeGenericType(parameterType);
                    var actionDelegate = Delegate.CreateDelegate(actionType, target, method);

                    // 直接调用泛型方法
                    RegisterEventGeneric(parameterType, actionDelegate, false);
                }
                else
                {
                    if (OS.IsDebugBuild())
                        EventBus.Logger.LogError(
                            $"方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但返回类型不是 void 或 Task。");
                    continue;
                }

                if (OS.IsDebugBuild())
                    EventBus.Logger.LogInfo($"自动注册事件: {parameterType.FullName} -> {type.Name}.{method.Name}");
            }
            catch (Exception ex)
            {
                if (OS.IsDebugBuild())
                    EventBus.Logger.LogError($"为事件 {parameterType.FullName} 创建委托给方法 {method.Name} 失败: {ex}");
            }
        }
    }

    /// <summary>
    /// 注册事件的通用方法
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handlerDelegate">处理程序委托</param>
    /// <param name="isAsync">是否为异步处理程序</param>
    private static void RegisterEventGeneric(Type eventType, Delegate handlerDelegate, bool isAsync)
    {
        try
        {
            if (isAsync)
            {
                // 对于异步处理程序，直接调用内部注册方法
                var funcType = typeof(Func<,>).MakeGenericType(eventType, typeof(Task));
                var typedHandler = Convert.ChangeType(handlerDelegate, funcType);
                
                // 调用内部的字典注册方法
                RegisterEventInternal(eventType, (Delegate)typedHandler);
            }
            else
            {
                // 对于同步处理程序，包装成异步
                var wrappedHandler = WrapSyncHandler(eventType, handlerDelegate);
                RegisterEventInternal(eventType, wrappedHandler);
            }

            if (OS.IsDebugBuild())
                EventBus.Logger.LogInfo($"成功注册事件: {eventType.FullName}, 异步: {isAsync}");
        }
        catch (Exception ex)
        {
            if (OS.IsDebugBuild())
                EventBus.Logger.LogError($"注册事件失败: {eventType.FullName}, 异常: {ex}");
        }
    }

    /// <summary>
    /// 内部注册方法，直接操作EventBus的字典
    /// </summary>
    private static void RegisterEventInternal(Type eventType, Delegate handler)
    {
        // 获取EventBus的私有字段_eventHandlers
        var eventHandlersField = typeof(EventBus).GetField("_eventHandlers", BindingFlags.NonPublic | BindingFlags.Static);
        if (eventHandlersField == null)
        {
            throw new InvalidOperationException("无法访问EventBus._eventHandlers字段");
        }

        var eventHandlers = (Dictionary<Type, Delegate>)eventHandlersField.GetValue(null)!;
        
        if (!eventHandlers.TryGetValue(eventType, out var existingHandler))
        {
            eventHandlers[eventType] = handler;
            if (OS.IsDebugBuild())
            {
                EventBus.Logger.LogInfo($"创建新的事件处理程序: {eventType.FullName}");
            }
        }
        else
        {
            eventHandlers[eventType] = Delegate.Combine(existingHandler, handler);
            if (OS.IsDebugBuild())
            {
                EventBus.Logger.LogInfo($"合并事件处理程序: {eventType.FullName}");
            }
        }

        if (OS.IsDebugBuild())
        {
            EventBus.Logger.LogInfo($"当前事件处理程序字典大小: {eventHandlers.Count}");
        }
    }

    /// <summary>
    /// 包装同步处理程序为异步
    /// </summary>
    private static Delegate WrapSyncHandler(Type eventType, Delegate syncHandler)
    {
        var method = typeof(EventBusGeneratedRegistration)
            .GetMethod(nameof(WrapAction), BindingFlags.NonPublic | BindingFlags.Static)
            ?.MakeGenericMethod(eventType);
        
        if (method == null)
        {
            throw new InvalidOperationException("无法找到WrapAction方法");
        }
        
        return (Delegate)method.Invoke(null, [syncHandler])!;
    }

    private static Func<T, Task> WrapAction<T>(Delegate actionDelegate)
    {
        var typedAction = (Action<T>)actionDelegate;
        return arg =>
        {
            try
            {
                typedAction(arg);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var tcs = new TaskCompletionSource<object>();
                tcs.SetException(ex);
                return tcs.Task.ContinueWith(_ => Task.CompletedTask, TaskScheduler.Default).Unwrap();
            }
        };
    }
}