using System.Reflection;
using System.Runtime.CompilerServices;
using CommonSDK.Logger;
using Godot;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace CommonSDK.Event;

/// <summary>
/// 事件基类，所有自定义事件都应继承此类
/// </summary>
public abstract class EventBusEvent { }

/// <summary>
/// 事件总线系统
/// <para>提供基于事件类型的发布-订阅模式实现</para>
/// <para>支持类型安全的事件处理和自动注册基于特性的事件处理程序</para>
/// </summary>
[Singleton(LoadMode.Lazy)]
public partial class EventBus : Singleton<EventBus>
{
    /// <summary>
    /// 存储所有已注册的事件处理程序
    /// </summary>
    private readonly Dictionary<Type, Delegate> _eventHandlers = new();

    /// <summary>
    /// 日志记录器
    /// </summary>
    public readonly LogHelper Logger = new("EventBus");

    /// <summary>
    /// 存储已经注册过的类型，避免重复注册
    /// </summary>
    private readonly HashSet<Type> _registeredTypes = [];

    /// <summary>
    /// 静态构造函数，初始化时注册当前程序集中的事件处理程序
    /// </summary>
    static EventBus()
    {
        RegisterStaticEventHandlers();
    }

    /// <summary>
    /// 注册静态事件处理程序
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RegisterStaticEventHandlers()
    {
        EventBusGeneratedRegistration.RegisterStaticEventHandlers(Instance);
    }

    /// <summary>
    /// 注册事件处理程序（异步）
    /// <para>如果事件已存在处理程序，则将新处理程序与现有处理程序组合</para>
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理程序</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RegisterEvent<TEvent>(Func<TEvent, Task> handler) where TEvent : EventBusEvent
    {
        var eventType = typeof(TEvent);
        if (!_eventHandlers.TryAdd(eventType, handler))
            _eventHandlers[eventType] = Delegate.Combine(_eventHandlers[eventType], handler);
    }

    /// <summary>
    /// 注册事件处理程序（同步）
    /// <para>如果事件已存在处理程序，则将新处理程序与现有处理程序组合</para>
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理程序</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RegisterEvent<TEvent>(Action<TEvent> handler) where TEvent : EventBusEvent
    {
        Func<TEvent, Task> asyncHandler = arg => 
        {
            handler(arg);
            return Task.CompletedTask;
        };
        
        RegisterEvent(asyncHandler);
    }

    /// <summary>
    /// 注销事件处理程序（异步）
    /// <para>如果移除后没有处理程序，则完全移除该事件</para>
    /// <para>可以注销同步和异步事件处理程序</para>
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">要注销的事件处理程序</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnregisterEvent<TEvent>(Func<TEvent, Task> handler) where TEvent : EventBusEvent
    {
        var eventType = typeof(TEvent);
        if (!_eventHandlers.TryGetValue(eventType, out Delegate existingHandler)) return;
        
        var newHandler = Delegate.Remove(existingHandler, handler);
        
        if (newHandler == existingHandler)
        {
            foreach (var existingDelegate in existingHandler.GetInvocationList())
            {
                if (existingDelegate is Func<TEvent, Task> existingAsyncHandler)
                {
                    if (IsWrappedSyncHandler(existingAsyncHandler, handler))
                    {
                        newHandler = Delegate.Remove(existingHandler, existingAsyncHandler);
                        break;
                    }
                }
            }
        }
        
        if (newHandler == null || newHandler.GetInvocationList().Length == 0)
        {
            _eventHandlers.Remove(eventType);
        }
        else
        {
            _eventHandlers[eventType] = newHandler;
        }
    }

    /// <summary>
    /// 注销事件处理程序（同步）
    /// <para>如果移除后没有处理程序，则完全移除该事件</para>
    /// <para>可以注销同步和异步事件处理程序</para>
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">要注销的事件处理程序</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnregisterEvent<TEvent>(Action<TEvent> handler) where TEvent : EventBusEvent
    {
        Func<TEvent, Task> asyncHandler = arg => 
        {
            handler(arg);
            return Task.CompletedTask;
        };
        
        UnregisterEvent(asyncHandler);
    }

    /// <summary>
    /// 检查是否是包装的同步处理程序
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="asyncHandler">异步处理程序</param>
    /// <param name="targetHandler">目标处理程序</param>
    /// <returns>如果是包装的同步处理程序返回true，否则返回false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsWrappedSyncHandler<TEvent>(Func<TEvent, Task> asyncHandler, Delegate targetHandler) 
        where TEvent : EventBusEvent
    {
        try
        {
            var asyncMethodInfo = asyncHandler.Method;
            
            if (asyncMethodInfo.Name.Contains("<"))
            {
                var target = asyncHandler.Target;
                var fields = target.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Action<TEvent>) || field.FieldType == typeof(Delegate))
                    {
                        var actualDelegate = field.GetValue(target) as Delegate;
                        if (actualDelegate != null && actualDelegate.Target == targetHandler.Target && 
                            actualDelegate.Method == targetHandler.Method)
                        {
                            return true;
                        }
                    }
                }
            }
            else if (asyncHandler.Target == targetHandler.Target && 
                     asyncHandler.Method == targetHandler.Method)
            {
                return true;
            }
        }
        catch (Exception)
        {
            // 忽略错误
        }
        
        return false;
    }

    /// <summary>
    /// 触发指定事件（异步）
    /// <para>调用所有注册到该事件的处理程序</para>
    /// <para>如果处理程序执行过程中出现异常，将被捕获并记录（仅在调试模式）</para>
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventArgs">事件参数</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task TriggerEventAsync<TEvent>(TEvent eventArgs) where TEvent : EventBusEvent
    {
        var eventType = typeof(TEvent);
        if (!_eventHandlers.TryGetValue(eventType, out var eventHandlerDelegate))
            return;

        if (eventHandlerDelegate == null) return;
        
        foreach (var handler in eventHandlerDelegate.GetInvocationList())
        {
            if (handler is Func<TEvent, Task> typedHandler)
            {
                try
                {
                    await typedHandler(eventArgs);
                }
                catch (Exception ex)
                {
                    if (OS.IsDebugBuild())
                    {
                        Logger.LogError($"执行事件 {eventType.Name} 时发生异常: {ex}");
                    }
                }
            }
            else
            {
                if (OS.IsDebugBuild())
                {
                    Logger.LogError(
                        $"事件 {eventType.Name} 的处理程序类型不匹配。预期 Func<{eventType.Name}, Task>，实际为 {handler.GetType().Name}");
                }
            }
        }
    }

    /// <summary>
    /// 触发指定事件（同步）
    /// <para>调用所有注册到该事件的处理程序</para>
    /// <para>如果处理程序执行过程中出现异常，将被捕获并记录（仅在调试模式）</para>
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventArgs">事件参数</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TriggerEvent<TEvent>(TEvent eventArgs) where TEvent : EventBusEvent
    {
        var eventType = typeof(TEvent);
        if (!_eventHandlers.TryGetValue(eventType, out var eventHandlerDelegate))
            return;

        if (eventHandlerDelegate == null) return;
        
        foreach (var handler in eventHandlerDelegate.GetInvocationList())
        {
            if (handler is Func<TEvent, Task> typedHandler)
            {
                try
                {
                    typedHandler(eventArgs).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    if (OS.IsDebugBuild())
                    {
                        Logger.LogError($"执行事件 {eventType.Name} 时发生异常: {ex}");
                    }
                }
            }
            else
            {
                if (OS.IsDebugBuild())
                {
                    Logger.LogError(
                        $"事件 {eventType.Name} 的处理程序类型不匹配。预期 Func<{eventType.Name}, Task>，实际为 {handler.GetType().Name}");
                }
            }
        }
    }

    /// <summary>
    /// 取消指定事件（移除所有处理程序）
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CancelEvent<TEvent>() where TEvent : EventBusEvent
    {
        var eventType = typeof(TEvent);
        if (!_eventHandlers.Remove(eventType)) return;
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo($"事件 {eventType.Name} 已被取消 (所有处理程序已移除)。");
        }
    }

    /// <summary>
    /// 注销指定对象的所有事件处理程序
    /// <para>用于在对象销毁前清理其所有事件订阅</para>
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnregisterAllEventsForObject(object targetObject)
    {
        if (targetObject is null)
        {
            if (OS.IsDebugBuild())
            {
                Logger.LogInfo("目标对象为 null，无法取消订阅。");
            }

            return;
        }

        var eventTypes = new List<Type>(_eventHandlers.Keys);

        foreach (var eventType in eventTypes)
        {
            if (!_eventHandlers.TryGetValue(eventType, out var eventHandlerDelegate) ||
                eventHandlerDelegate == null) continue;
            Delegate newDelegate = null;
            var modified = false;
            foreach (var handler in eventHandlerDelegate.GetInvocationList())
            {
                if (handler.Target == targetObject)
                {
                    modified = true;
                }
                else
                {
                    newDelegate = Delegate.Combine(newDelegate, handler);
                }
            }

            if (!modified) continue;
            if (newDelegate == null)
            {
                _eventHandlers.Remove(eventType);
            }
            else
            {
                _eventHandlers[eventType] = newDelegate;
            }
        }

        if (OS.IsDebugBuild())
        {
            Logger.LogInfo($"已为 {targetObject} 注销所有相关事件订阅。");
        }
    }

    /// <summary>
    /// 注销所有事件处理程序
    /// <para>完全清空事件总线</para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnregisterAllEvents()
    {
        _eventHandlers.Clear();
        _registeredTypes.Clear();
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo("所有事件订阅已被注销。");
        }
    }

    /// <summary>
    /// 从目标对象中自动注册带有特性标记的事件处理程序
    /// <para>扫描对象的所有方法，查找带有 EventSubscribeAttribute 特性的方法</para>
    /// <para>符合条件的方法必须有一个参数（继承自 EventBusEvent）且返回类型为 void 或 Task</para>
    /// </summary>
    /// <param name="target">包含事件处理程序的目标对象</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RegisterEventHandlersFromAttributes(object target)
    {
        if (target == null)
        {
            if (OS.IsDebugBuild()) Logger.LogError("RegisterEventHandlersFromAttributes: 目标对象为 null。");
            return;
        }

        var targetType = target.GetType();
        
        if (_registeredTypes.Contains(targetType))
            return;
            
        EventBusGeneratedRegistration.RegisterEventHandlers(this, target);
        
        _registeredTypes.Add(targetType);
    }

    /// <summary>
    /// 自动注册对象的事件处理程序
    /// <para>如果对象尚未注册，则自动注册其带有 EventSubscribeAttribute 特性的方法</para>
    /// </summary>
    /// <param name="target">目标对象</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AutoRegister(object target)
    {
        if (target == null)
        {
            if (OS.IsDebugBuild()) Logger.LogError("AutoRegister: 目标对象为 null。");
            return;
        }

        var targetType = target.GetType();
        
        if (_registeredTypes.Contains(targetType))
            return;
            
        EventBusGeneratedRegistration.RegisterEventHandlers(this, target);
        
        _registeredTypes.Add(targetType);
    }
}

/// <summary>
/// 事件订阅特性
/// <para>用于标记方法作为特定事件的处理程序</para>
/// <para>被标记的方法必须有一个参数（继承自 EventBusEvent）且返回类型为 void 或 Task</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class EventSubscribeAttribute : Attribute
{
    /// <summary>
    /// 创建新的事件订阅特性
    /// </summary>
    public EventSubscribeAttribute()
    {
        // 不再需要事件名称，因为我们将使用事件类型
    }
}

/// <summary>
/// 自动注册事件处理程序特性
/// <para>标记了此特性的类，在实例化时会自动注册其事件处理程序</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public class AutoRegisterEventsAttribute : Attribute
{
}

/// <summary>
/// 源码生成器生成的注册类，用于替代反射
/// </summary>
public static class EventBusGeneratedRegistration
{
    /// <summary>
    /// 注册静态事件处理程序
    /// </summary>
    /// <param name="eventBus">事件总线实例</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterStaticEventHandlers(EventBus eventBus)
    {
        RegisterStaticEventHandlersWithReflection(eventBus);
    }
    
    /// <summary>
    /// 注册事件处理程序
    /// </summary>
    /// <param name="eventBus">事件总线实例</param>
    /// <param name="target">目标对象</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEventHandlers(EventBus eventBus, object target)
    {
        RegisterEventHandlersWithReflection(eventBus, target);
    }
    
    /// <summary>
    /// 使用反射注册静态事件处理程序（作为后备方案）
    /// </summary>
    /// <param name="eventBus">事件总线实例</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RegisterStaticEventHandlersWithReflection(EventBus eventBus)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    foreach (var method in methods)
                    {
                        var attributes = method.GetCustomAttributes(typeof(EventSubscribeAttribute), false);
                        if (attributes.Length == 0) continue;
                        
                        var parameters = method.GetParameters();
                        
                        if (parameters.Length != 1)
                        {
                            if (OS.IsDebugBuild())
                                eventBus.Logger.LogError(
                                    $"静态方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但参数数量不为1。");
                            continue;
                        }
                        
                        var parameterType = parameters[0].ParameterType;
                        if (!typeof(EventBusEvent).IsAssignableFrom(parameterType))
                        {
                            if (OS.IsDebugBuild())
                                eventBus.Logger.LogError(
                                    $"静态方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但参数类型 {parameterType.Name} 不继承自 EventBusEvent。");
                            continue;
                        }
                        
                        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
                        {
                            if (OS.IsDebugBuild())
                                eventBus.Logger.LogError(
                                    $"静态方法 {method.Name} 在类 {type.Name} 中带有 EventSubscribeAttribute，但返回类型不是 void 或 Task。");
                            continue;
                        }
                        
                        try
                        {
                            Delegate handlerDelegate;
                            if (method.ReturnType == typeof(void))
                            {
                                var actionType = typeof(Action<>).MakeGenericType(parameterType);
                                handlerDelegate = Delegate.CreateDelegate(actionType, method);
                            }
                            else
                            {
                                handlerDelegate = Delegate.CreateDelegate(
                                    typeof(Func<,>).MakeGenericType(parameterType, typeof(Task)),
                                    method);
                            }
                            
                            var registerMethod = typeof(EventBus).GetMethod(nameof(EventBus.RegisterEvent), 
                                BindingFlags.Public | BindingFlags.Instance, 
                                null, 
                                new[] { handlerDelegate.GetType() }, 
                                null);
                            
                            if (registerMethod != null)
                            {
                                registerMethod.MakeGenericMethod(parameterType).Invoke(eventBus, new[] { handlerDelegate });
                            }
                            
                            if (OS.IsDebugBuild())
                                eventBus.Logger.LogInfo($"自动注册静态事件: {parameterType.Name} -> {type.Name}.{method.Name}");
                        }
                        catch (Exception ex)
                        {
                            if (OS.IsDebugBuild()) eventBus.Logger.LogError($"为静态事件 {parameterType.Name} 创建委托给方法 {method.Name} 失败: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (OS.IsDebugBuild()) eventBus.Logger.LogError($"扫描程序集 {assembly.FullName} 时发生异常: {ex}");
            }
        }
    }
    
    /// <summary>
    /// 使用反射注册事件处理程序（作为后备方案）
    /// </summary>
    /// <param name="eventBus">事件总线实例</param>
    /// <param name="target">目标对象</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RegisterEventHandlersWithReflection(EventBus eventBus, object target)
    {
        var methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        foreach (var method in methods)
        {
            var attributes = method.GetCustomAttributes(typeof(EventSubscribeAttribute), false);
            foreach (var attribute in attributes)
            {
                if (attribute is not EventSubscribeAttribute) continue;

                var parameters = method.GetParameters();

                if (parameters.Length != 1)
                {
                    if (OS.IsDebugBuild())
                        eventBus.Logger.LogError(
                            $"方法 {method.Name} 在类 {target.GetType().Name} 中带有 EventSubscribeAttribute，但参数数量不为1。");
                    continue;
                }

                var parameterType = parameters[0].ParameterType;
                if (!typeof(EventBusEvent).IsAssignableFrom(parameterType))
                {
                    if (OS.IsDebugBuild())
                        eventBus.Logger.LogError(
                            $"方法 {method.Name} 在类 {target.GetType().Name} 中带有 EventSubscribeAttribute，但参数类型 {parameterType.Name} 不继承自 EventBusEvent。");
                    continue;
                }

                if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task))
                {
                    if (OS.IsDebugBuild())
                        eventBus.Logger.LogError(
                            $"方法 {method.Name} 在类 {target.GetType().Name} 中带有 EventSubscribeAttribute，但返回类型不是 void 或 Task。");
                    continue;
                }

                try
                {
                    var delegateTarget = method.IsStatic ? null : target;
                    
                    Delegate handlerDelegate;
                    if (method.ReturnType == typeof(void))
                    {
                        var actionType = typeof(Action<>).MakeGenericType(parameterType);
                        var actionDelegate = Delegate.CreateDelegate(actionType, delegateTarget, method);
                        
                        handlerDelegate = (Func<object, Task>)(arg => 
                        {
                            actionDelegate.DynamicInvoke(arg);
                            return Task.CompletedTask;
                        });
                    }
                    else
                    {
                        handlerDelegate = Delegate.CreateDelegate(
                            typeof(Func<,>).MakeGenericType(parameterType, typeof(Task)),
                            delegateTarget,
                            method);
                    }

                    var registerMethod = typeof(EventBus).GetMethod(nameof(EventBus.RegisterEvent), 
                        BindingFlags.Public | BindingFlags.Instance, 
                        null, 
                        new[] { handlerDelegate.GetType() }, 
                        null);
                    
                    if (registerMethod != null)
                    {
                        registerMethod.MakeGenericMethod(parameterType).Invoke(eventBus, new[] { handlerDelegate });
                    }

                    if (OS.IsDebugBuild())
                        eventBus.Logger.LogInfo($"自动注册事件: {parameterType.Name} -> {target.GetType().Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    if (OS.IsDebugBuild()) eventBus.Logger.LogError($"为事件 {parameterType.Name} 创建委托给方法 {method.Name} 失败: {ex}");
                }
            }
        }
    }
}