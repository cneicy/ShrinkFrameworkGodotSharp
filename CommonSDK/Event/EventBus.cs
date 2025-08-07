using System.Reflection;
using System.Runtime.CompilerServices;
using CommonSDK.Logger;
using Godot;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace CommonSDK.Event;

/// <summary>
/// 事件基类，所有自定义事件都应继承此类
/// </summary>
public abstract class EventBase
{
}

/// <summary>
/// 事件总线系统
/// <para>提供基于事件类型的发布-订阅模式实现</para>
/// <para>支持类型安全的事件处理和自动注册基于特性的事件处理程序</para>
/// </summary>
/// <summary>
/// 事件总线系统（静态版本）
/// <para>提供基于事件类型的发布-订阅模式实现</para>
/// <para>支持类型安全的事件处理和自动注册基于特性的事件处理程序</para>
/// </summary>
public static class EventBus
{
    /// <summary>
    /// 存储所有已注册的事件处理程序
    /// </summary>
    private static readonly Dictionary<Type, Delegate> _eventHandlers = new();

    /// <summary>
    /// 日志记录器
    /// </summary>
    public static readonly LogHelper Logger = new("EventBus");

    /// <summary>
    /// 存储已经注册过的类型，避免重复注册
    /// </summary>
    private static readonly HashSet<Type> _registeredTypes = [];

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
        EventBusGeneratedRegistration.RegisterStaticEventHandlers();
    }

    /// <summary>
    /// 注册事件处理程序（异步）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Func<TEvent, Task> handler) where TEvent : EventBase
    {
        var eventType = typeof(TEvent);
    
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo($"正在注册事件处理程序: {eventType.FullName}");
        }
    
        if (!_eventHandlers.TryGetValue(eventType, out var existingHandler))
        {
            _eventHandlers[eventType] = handler;
            if (OS.IsDebugBuild())
            {
                Logger.LogInfo($"创建新的事件处理程序: {eventType.FullName}");
            }
        }
        else
        {
            _eventHandlers[eventType] = Delegate.Combine(existingHandler, handler);
            if (OS.IsDebugBuild())
            {
                Logger.LogInfo($"合并事件处理程序: {eventType.FullName}");
            }
        }
    
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo($"当前事件处理程序字典大小: {_eventHandlers.Count}");
        }
    }

    /// <summary>
    /// 注册事件处理程序（同步）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Action<TEvent> handler) where TEvent : EventBase
    {
        RegisterEvent((Func<TEvent, Task>)AsyncHandler);
        return;

        Task AsyncHandler(TEvent arg)
        {
            handler(arg);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 注销事件处理程序（异步）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterEvent<TEvent>(Func<TEvent, Task> handler) where TEvent : EventBase
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
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterEvent<TEvent>(Action<TEvent> handler) where TEvent : EventBase
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWrappedSyncHandler<TEvent>(Func<TEvent, Task> asyncHandler, Delegate targetHandler)
        where TEvent : EventBase
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
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static async Task TriggerEventAsync<TEvent>(TEvent eventArgs) where TEvent : EventBase
{
    var eventType = typeof(TEvent);
    
    if (OS.IsDebugBuild())
    {
        Logger.LogInfo($"准备触发事件: {eventType.FullName}");
        Logger.LogInfo($"当前已注册的事件类型数量: {_eventHandlers.Count}");
        
        // 输出所有已注册的事件类型
        foreach (var registeredType in _eventHandlers.Keys)
        {
            Logger.LogInfo($"已注册事件: {registeredType.FullName}");
        }
    }
    
    if (!_eventHandlers.TryGetValue(eventType, out var eventHandlerDelegate))
    {
        if (OS.IsDebugBuild())
        {
            Logger.LogWarn($"没有找到事件 {eventType.FullName} 的处理程序");
        }
        return;
    }

    // 其余代码保持不变...
    if (eventHandlerDelegate == null) 
    {
        if (OS.IsDebugBuild())
        {
            Logger.LogWarn($"事件 {eventType.Name} 的处理程序为 null");
        }
        return;
    }

    var handlers = eventHandlerDelegate.GetInvocationList();
    
    if (OS.IsDebugBuild())
    {
        Logger.LogInfo($"找到 {handlers.Length} 个事件处理程序");
    }

    foreach (var handler in handlers)
    {
        if (handler is Func<TEvent, Task> typedHandler)
        {
            try
            {
                if (OS.IsDebugBuild())
                {
                    Logger.LogInfo($"执行处理程序: {handler.Method.Name}");
                }
                
                await typedHandler(eventArgs);
                
                if (OS.IsDebugBuild())
                {
                    Logger.LogInfo($"处理程序 {handler.Method.Name} 执行完成");
                }
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
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TriggerEvent<TEvent>(TEvent eventArgs) where TEvent : EventBase
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CancelEvent<TEvent>() where TEvent : EventBase
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
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterAllEventsForObject(object targetObject)
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
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterAllEvents()
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
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEventHandlersFromAttributes(object target)
    {
        if (target == null)
        {
            if (OS.IsDebugBuild()) Logger.LogError("RegisterEventHandlersFromAttributes: 目标对象为 null。");
            return;
        }

        var targetType = target.GetType();

        if (_registeredTypes.Contains(targetType))
            return;

        EventBusGeneratedRegistration.RegisterEventHandlers(target);

        _registeredTypes.Add(targetType);
    }

    /// <summary>
    /// 自动注册对象的事件处理程序
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AutoRegister(object target)
    {
        if (target == null)
        {
            if (OS.IsDebugBuild()) Logger.LogError("AutoRegister: 目标对象为 null。");
            return;
        }

        var targetType = target.GetType();

        if (_registeredTypes.Contains(targetType))
            return;
        
        EventBusGeneratedRegistration.RegisterEventHandlers(target);

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
[AttributeUsage(AttributeTargets.Class)]
public class EventBusSubscriberAttribute : Attribute
{
}