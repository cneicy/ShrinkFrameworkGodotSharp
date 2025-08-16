using System.Reflection;
using System.Runtime.CompilerServices;
using CommonSDK.Logger;
using Godot;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace CommonSDK.Event;

/// <summary>
/// 事件优先级枚举 - 类似 Forge EventPriority
/// </summary>
public enum EventPriority
{
    /// <summary>
    /// 最高优先级 - 最先执行，用于最重要的修改
    /// </summary>
    HIGHEST = 0,
    
    /// <summary>
    /// 高优先级 - 早期执行，用于重要的修改
    /// </summary>
    HIGH = 1,
    
    /// <summary>
    /// 普通优先级 - 默认优先级，用于一般的修改
    /// </summary>
    NORMAL = 2,
    
    /// <summary>
    /// 低优先级 - 后期执行，用于不太重要的修改
    /// </summary>
    LOW = 3,
    
    /// <summary>
    /// 最低优先级 - 最后执行，用于清理和收尾工作
    /// </summary>
    LOWEST = 4,
    
    /// <summary>
    /// 监控优先级 - 在所有处理完成后执行，用于监控和日志记录
    /// 在此阶段不应该修改事件状态
    /// </summary>
    MONITOR = 5
}

/// <summary>
/// 事件结果枚举 - 类似 Forge Event.Result
/// </summary>
public enum EventResult
{
    /// <summary>
    /// 默认行为 - 使用原版/默认逻辑
    /// </summary>
    DEFAULT,
    
    /// <summary>
    /// 允许操作 - 强制允许，即使原本不允许
    /// </summary>
    ALLOW,
    
    /// <summary>
    /// 拒绝操作 - 强制拒绝，即使原本允许
    /// </summary>
    DENY
}

/// <summary>
/// 可取消的事件特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CancelableAttribute : Attribute
{
}

/// <summary>
/// 有结果的事件特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HasResultAttribute : Attribute
{
}

/// <summary>
/// 事件处理程序信息
/// </summary>
public class EventHandlerInfo
{
    public Delegate Handler { get; }
    public EventPriority Priority { get; }
    public int NumericPriority { get; }
    public bool ReceiveCanceled { get; }
    public object? Target { get; }
    public MethodInfo Method { get; }
    public string DebugInfo { get; }
    public Type DeclaringType { get; }
    public string MethodName { get; }

    public EventHandlerInfo(Delegate handler, EventPriority priority, int numericPriority, bool receiveCanceled, string debugInfo = "")
    {
        Handler = handler;
        Priority = priority;
        NumericPriority = numericPriority;
        ReceiveCanceled = receiveCanceled;
        Target = handler.Target;
        Method = handler.Method;
        DebugInfo = debugInfo;
        DeclaringType = Method.DeclaringType ?? typeof(object);
        MethodName = Method.Name;
    }
}

/// <summary>
/// 监听器列表 - 模拟 Forge ListenerList
/// </summary>
public class ListenerList
{
    private readonly List<EventHandlerInfo> _handlers = new();
    private readonly object _lock = new();
    private bool _needsSort = true;

    public void Add(Delegate handler, EventPriority priority, int numericPriority, bool receiveCanceled, string debugInfo = "")
    {
        lock (_lock)
        {
            _handlers.Add(new EventHandlerInfo(handler, priority, numericPriority, receiveCanceled, debugInfo));
            _needsSort = true;
        }
    }

    public bool Remove(Delegate handler)
    {
        lock (_lock)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i].Handler.Equals(handler))
                {
                    _handlers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }

    public void RemoveTarget(object target)
    {
        lock (_lock)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i].Target == target)
                {
                    _handlers.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// 获取按优先级排序的处理程序数组
    /// </summary>
    public EventHandlerInfo[] GetSortedHandlers()
    {
        lock (_lock)
        {
            if (_needsSort)
            {
                // 首先按EventPriority排序，然后按数字优先级排序
                _handlers.Sort((a, b) =>
                {
                    var priorityCompare = a.Priority.CompareTo(b.Priority);
                    if (priorityCompare != 0) return priorityCompare;
                    return b.NumericPriority.CompareTo(a.NumericPriority); // 数字优先级降序
                });
                _needsSort = false;
            }
            
            return _handlers.ToArray();
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _handlers.Count;
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _handlers.Clear();
            _needsSort = false;
        }
    }

    /// <summary>
    /// 获取所有处理程序的详细信息
    /// </summary>
    public EventHandlerInfo[] GetAllHandlers()
    {
        lock (_lock)
        {
            return _handlers.ToArray();
        }
    }

    public string GetDebugInfo()
    {
        lock (_lock)
        {
            var sb = new System.Text.StringBuilder();
            var sortedHandlers = GetSortedHandlers();
            
            for (int i = 0; i < sortedHandlers.Length; i++)
            {
                var handler = sortedHandlers[i];
                sb.AppendLine($"  [{i + 1}] {handler.DeclaringType?.Name}.{handler.MethodName} - Priority: {handler.Priority}({handler.NumericPriority}), ReceiveCanceled: {handler.ReceiveCanceled}");
            }
            
            return sb.ToString();
        }
    }
}

/// <summary>
/// 不支持的操作异常（C# 内置异常的替代）
/// </summary>
public class UnsupportedOperationException : InvalidOperationException
{
    public UnsupportedOperationException() : base() { }
    public UnsupportedOperationException(string message) : base(message) { }
    public UnsupportedOperationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// 事件基类，所有自定义事件都应继承此类
/// </summary>
/// <remarks>
/// 支持取消、结果设置和优先级处理的增强事件基类
/// </remarks>
public abstract class EventBase
{
    private bool _canceled = false;
    private EventResult _result = EventResult.DEFAULT;
    private readonly bool _cancelable;
    private readonly bool _hasResult;
    private EventPriority? _phase = null;
    private readonly ListenerList _listenerList;

    /// <summary>
    /// 当前正在处理此事件的处理程序信息
    /// </summary>
    public EventHandlerInfo? CurrentHandler { get; internal set; }

    /// <summary>
    /// 事件触发的时间戳
    /// </summary>
    public DateTime EventTime { get; } = DateTime.UtcNow;

    /// <summary>
    /// 事件的唯一标识符
    /// </summary>
    public Guid EventId { get; } = Guid.NewGuid();

    protected EventBase()
    {
        _cancelable = GetType().GetCustomAttribute<CancelableAttribute>() != null;
        _hasResult = GetType().GetCustomAttribute<HasResultAttribute>() != null;
        _listenerList = new ListenerList();
        Setup();
    }

    /// <summary>
    /// 由基础构造函数调用，用于设置各种功能
    /// </summary>
    protected virtual void Setup()
    {
        // 子类可以重写此方法进行额外设置
    }

    /// <summary>
    /// 检查事件是否可以被取消
    /// </summary>
    public bool IsCancelable => _cancelable;

    /// <summary>
    /// 检查事件是否有结果
    /// </summary>
    public bool HasResult => _hasResult;

    /// <summary>
    /// 获取或设置事件是否被取消
    /// </summary>
    public bool IsCanceled
    {
        get => _canceled;
        set
        {
            if (!_cancelable)
            {
                throw new UnsupportedOperationException($"事件 {GetType().Name} 不支持取消操作");
            }
            _canceled = value;
        }
    }

    /// <summary>
    /// 获取或设置事件结果
    /// </summary>
    public EventResult Result
    {
        get => _result;
        set
        {
            if (!_hasResult)
            {
                throw new InvalidOperationException($"事件 {GetType().Name} 不支持结果设置");
            }
            _result = value;
        }
    }

    /// <summary>
    /// 获取当前事件阶段
    /// </summary>
    public EventPriority? Phase => _phase;

    /// <summary>
    /// 设置事件阶段（内部使用）
    /// </summary>
    internal void SetPhase(EventPriority value)
    {
        if (_phase != null && _phase.Value.CompareTo(value) >= 0)
        {
            throw new ArgumentException($"尝试将事件阶段设置为 {value}，但当前已经是 {_phase}");
        }
        _phase = value;
    }

    /// <summary>
    /// 设置事件为已取消
    /// </summary>
    public void SetCanceled(bool canceled)
    {
        IsCanceled = canceled;
    }

    /// <summary>
    /// 设置事件结果
    /// </summary>
    public void SetResult(EventResult result)
    {
        Result = result;
    }

    /// <summary>
    /// 获取包含所有已注册到此事件的监听器的ListenerList对象
    /// </summary>
    public ListenerList GetListenerList()
    {
        return _listenerList;
    }

    /// <summary>
    /// 获取当前事件的所有订阅者信息
    /// </summary>
    public EventHandlerInfo[] GetSubscribers()
    {
        return _listenerList.GetAllHandlers();
    }

    /// <summary>
    /// 获取事件的详细调试信息
    /// </summary>
    public string GetEventDebugInfo()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"事件类型: {GetType().FullName}");
        sb.AppendLine($"事件ID: {EventId}");
        sb.AppendLine($"触发时间: {EventTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
        sb.AppendLine($"可取消: {IsCancelable}");
        sb.AppendLine($"有结果: {HasResult}");
        sb.AppendLine($"已取消: {IsCanceled}");
        sb.AppendLine($"结果: {Result}");
        sb.AppendLine($"当前阶段: {Phase}");
        
        if (CurrentHandler != null)
        {
            sb.AppendLine($"当前处理程序: {CurrentHandler.DeclaringType?.Name}.{CurrentHandler.MethodName}");
        }
        
        sb.AppendLine($"订阅者数量: {_listenerList.Count}");
        sb.AppendLine("订阅者详情:");
        sb.Append(_listenerList.GetDebugInfo());
        
        return sb.ToString();
    }
}

/// <summary>
/// 事件总线系统（静态版本）
/// </summary>
/// <remarks>
/// <para>提供基于事件类型的发布-订阅模式实现</para>
/// <para>支持类型安全的事件处理和自动注册基于特性的事件处理程序</para>
/// <para>支持处理程序优先级排序，数值越大优先级越高</para>
/// <para>支持同步和异步事件处理程序</para>
/// <para>提供自动注册和手动注册两种方式</para>
/// <para>线程安全设计，支持多线程环境下的事件处理</para>
/// </remarks>
public static class EventBus
{
    /// <summary>
    /// 存储所有已注册的事件处理程序（带优先级）
    /// </summary>
    private static readonly Dictionary<Type, ListenerList> EventHandlers = new();

    /// <summary>
    /// 日志记录器实例
    /// </summary>
    public static readonly LogHelper Logger = new("EventBus");

    /// <summary>
    /// 存储已经注册过的实例，避免重复注册（实例级别）
    /// </summary>
    private static readonly HashSet<object> RegisteredInstances = new();

    /// <summary>
    /// 实例注册操作的线程安全锁
    /// </summary>
    private static readonly object InstanceLock = new();
    
    /// <summary>
    /// 标记是否已确保自动注册管理器初始化
    /// </summary>
    private static bool _hasEnsuredAutoRegInit;
    
    /// <summary>
    /// 自动注册初始化操作的线程安全锁
    /// </summary>
    private static readonly object AutoRegInitLock = new();
    
    /// <summary>
    /// 确保自动注册管理器已初始化
    /// </summary>
    private static void EnsureAutoManagerInitialized()
    {
        if (_hasEnsuredAutoRegInit) return;
        
        lock (AutoRegInitLock)
        {
            if (_hasEnsuredAutoRegInit) return;
            EventAutoRegHelper.EnsureInitialized();
            _hasEnsuredAutoRegInit = true;
        }
    }

    /// <summary>
    /// 静态构造函数，初始化时注册当前程序集中的静态事件处理程序
    /// </summary>
    static EventBus()
    {
        RegStaticEventHandler();
    }

    /// <summary>
    /// 注册静态事件处理程序
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RegStaticEventHandler()
    {
        EventBusRegHelper.RegStaticEventHandler();
    }

    /// <summary>
    /// 注册事件处理程序（异步版本，支持EventPriority）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Func<TEvent, Task> handler, EventPriority priority = EventPriority.NORMAL, bool receiveCanceled = false) where TEvent : EventBase
    {
        RegisterEventInternal(typeof(TEvent), handler, priority, 0, receiveCanceled, $"Manual Async Handler (Priority: {priority})");
    }

    /// <summary>
    /// 注册事件处理程序（异步版本，向后兼容数字优先级）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Func<TEvent, Task> handler, int priority = 0) where TEvent : EventBase
    {
        var eventPriority = ConvertToEventPriority(priority);
        RegisterEventInternal(typeof(TEvent), handler, eventPriority, priority, false, $"Manual Async Handler (Priority: {priority})");
    }

    /// <summary>
    /// 注册同步事件处理程序（支持EventPriority）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Action<TEvent> handler, EventPriority priority = EventPriority.NORMAL, bool receiveCanceled = false) where TEvent : EventBase
    {
        Func<TEvent, Task> asyncHandler = arg =>
        {
            handler(arg);
            return Task.CompletedTask;
        };
        RegisterEventInternal(typeof(TEvent), asyncHandler, priority, 0, receiveCanceled, $"Manual Sync Handler (Priority: {priority})");
    }
    
    /// <summary>
    /// 注册同步事件处理程序（向后兼容数字优先级）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Action<TEvent> handler, int priority = 0) where TEvent : EventBase
    {
        var eventPriority = ConvertToEventPriority(priority);
        Func<TEvent, Task> asyncHandler = arg =>
        {
            handler(arg);
            return Task.CompletedTask;
        };
        RegisterEventInternal(typeof(TEvent), asyncHandler, eventPriority, priority, false, $"Manual Sync Handler (Priority: {priority})");
    }
    
    private static EventPriority ConvertToEventPriority(int numericPriority)
    {
        return numericPriority switch
        {
            >= 100 => EventPriority.HIGHEST,
            >= 50 => EventPriority.HIGH,
            > 0 => EventPriority.NORMAL,
            >= -50 => EventPriority.LOW,
            _ => EventPriority.LOWEST
        };
    }

    /// <summary>
    /// 内部事件注册方法
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegisterEventInternal(Type eventType, Delegate handler, EventPriority priority, int numericPriority, bool receiveCanceled, string debugInfo = "")
    {
        lock (EventHandlers)
        {
            if (!EventHandlers.TryGetValue(eventType, out var collection))
            {
                collection = new ListenerList();
                EventHandlers[eventType] = collection;
                
                if (OS.IsDebugBuild())
                {
                    Logger.LogInfo($"创建新的事件处理程序集合: {eventType.FullName}");
                }
            }

            collection.Add(handler, priority, numericPriority, receiveCanceled, debugInfo);

            if (OS.IsDebugBuild())
            {
                Logger.LogInfo($"成功注册事件: {eventType.FullName}, 优先级: {priority}({numericPriority}), 接收已取消: {receiveCanceled}");
            }
        }
    }

    /// <summary>
    /// 注销事件处理程序（异步版本）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterEvent<TEvent>(Func<TEvent, Task> handler) where TEvent : EventBase
    {
        var eventType = typeof(TEvent);
        lock (EventHandlers)
        {
            if (EventHandlers.TryGetValue(eventType, out var collection))
            {
                collection.Remove(handler);
                if (collection.Count == 0)
                {
                    EventHandlers.Remove(eventType);
                }
            }
        }
    }

    /// <summary>
    /// 注销事件处理程序（同步版本）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterEvent<TEvent>(Action<TEvent> handler) where TEvent : EventBase
    {
        var eventType = typeof(TEvent);
        lock (EventHandlers)
        {
            if (EventHandlers.TryGetValue(eventType, out var collection))
            {
                // 查找对应的包装后的异步处理程序
                var handlers = collection.GetSortedHandlers();
                foreach (var handlerInfo in handlers)
                {
                    if (IsWrappedSyncHandler(handlerInfo.Handler, handler))
                    {
                        collection.Remove(handlerInfo.Handler);
                        break;
                    }
                }
                
                if (collection.Count == 0)
                {
                    EventHandlers.Remove(eventType);
                }
            }
        }
    }

    /// <summary>
    /// 检查是否是包装的同步处理程序
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWrappedSyncHandler(Delegate asyncHandler, Delegate targetHandler)
    {
        try
        {
            var asyncMethodInfo = asyncHandler.Method;

            if (asyncMethodInfo.Name.Contains("<"))
            {
                var target = asyncHandler.Target;
                if (target == null) return false;
                
                var fields = target.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(Action<>) || field.FieldType == typeof(Delegate))
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
            // 忽略反射操作中的异常
        }

        return false;
    }

    /// <summary>
    /// 异步触发指定类型的事件 - 支持阶段处理和取消机制
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<bool> TriggerEventAsync<TEvent>(TEvent eventArgs) where TEvent : EventBase
    {
        EnsureAutoManagerInitialized();
        var eventType = typeof(TEvent);
        
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo($"准备触发事件: {eventType.FullName} (ID: {eventArgs.EventId})");
        }
        
        ListenerList? collection;
        lock (EventHandlers)
        {
            if (!EventHandlers.TryGetValue(eventType, out collection))
            {
                if (OS.IsDebugBuild())
                {
                    Logger.LogWarn($"没有找到事件 {eventType.FullName} 的处理程序");
                }
                return false;
            }
        }

        if (collection == null) 
        {
            if (OS.IsDebugBuild())
            {
                Logger.LogWarn($"事件 {eventType.Name} 的处理程序集合为 null");
            }
            return false;
        }

        // 将处理程序信息同步到事件的监听器列表
        var handlers = collection.GetSortedHandlers();
        foreach (var handler in handlers)
        {
            eventArgs.GetListenerList().Add(handler.Handler, handler.Priority, handler.NumericPriority, handler.ReceiveCanceled, handler.DebugInfo);
        }
        
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo($"找到 {handlers.Length} 个事件处理程序");
        }

        bool wasHandled = false;

        foreach (var handlerInfo in handlers)
        {
            // 设置当前处理程序信息
            eventArgs.CurrentHandler = handlerInfo;
            
            // 设置事件阶段
            eventArgs.SetPhase(handlerInfo.Priority);

            // 检查是否应该跳过已取消的事件
            if (eventArgs.IsCancelable && eventArgs.IsCanceled && !handlerInfo.ReceiveCanceled)
            {
                if (OS.IsDebugBuild())
                {
                    Logger.LogInfo($"跳过处理程序 {handlerInfo.DeclaringType?.Name}.{handlerInfo.MethodName}（事件已取消且处理程序不接收已取消事件）");
                }
                continue;
            }

            if (handlerInfo.Handler is Func<TEvent, Task> typedHandler)
            {
                try
                {
                    if (OS.IsDebugBuild())
                    {
                        Logger.LogInfo($"执行处理程序: {handlerInfo.DeclaringType?.Name}.{handlerInfo.MethodName} (优先级: {handlerInfo.Priority})");
                    }
                    
                    await typedHandler(eventArgs);
                    wasHandled = true;
                    
                    if (OS.IsDebugBuild())
                    {
                        Logger.LogInfo($"处理程序执行完成: {handlerInfo.DeclaringType?.Name}.{handlerInfo.MethodName}");
                        if (eventArgs.IsCancelable && eventArgs.IsCanceled)
                        {
                            Logger.LogInfo($"事件已被取消");
                        }
                        if (eventArgs.HasResult && eventArgs.Result != EventResult.DEFAULT)
                        {
                            Logger.LogInfo($"事件结果已设置为: {eventArgs.Result}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (OS.IsDebugBuild())
                    {
                        Logger.LogError($"执行事件 {eventType.Name} 处理程序时发生异常: {ex}");
                    }
                }
            }
            else
            {
                if (OS.IsDebugBuild())
                {
                    Logger.LogError($"事件 {eventType.Name} 的处理程序类型不匹配。预期 Func<{eventType.Name}, Task>，实际为 {handlerInfo.Handler.GetType().Name}");
                }
            }
        }

        // 清除当前处理程序信息
        eventArgs.CurrentHandler = null;

        return wasHandled;
    }

    /// <summary>
    /// 同步触发指定类型的事件
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TriggerEvent<TEvent>(TEvent eventArgs) where TEvent : EventBase
    {
        return TriggerEventAsync(eventArgs).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// 获取指定事件类型的所有订阅者信息
    /// </summary>
    public static EventHandlerInfo[] GetEventSubscribers<TEvent>() where TEvent : EventBase
    {
        var eventType = typeof(TEvent);
        lock (EventHandlers)
        {
            if (EventHandlers.TryGetValue(eventType, out var collection))
            {
                return collection.GetAllHandlers();
            }
            return Array.Empty<EventHandlerInfo>();
        }
    }
    
    /// <summary>
    /// 获取指定事件类型的监听器列表
    /// </summary>
    public static ListenerList GetListenerList<TEvent>() where TEvent : EventBase
    {
        var eventType = typeof(TEvent);
        lock (EventHandlers)
        {
            if (EventHandlers.TryGetValue(eventType, out var collection))
            {
                return collection;
            }
            return new ListenerList();
        }
    }

    /// <summary>
    /// 取消指定事件类型的所有处理程序
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CancelEvent<TEvent>() where TEvent : EventBase
    {
        var eventType = typeof(TEvent);
        lock (EventHandlers)
        {
            EventHandlers.Remove(eventType);
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

        lock (EventHandlers)
        {
            var eventTypesToRemove = new List<Type>();
            
            foreach (var kvp in EventHandlers)
            {
                kvp.Value.RemoveTarget(targetObject);
                if (kvp.Value.Count == 0)
                {
                    eventTypesToRemove.Add(kvp.Key);
                }
            }

            foreach (var eventType in eventTypesToRemove)
            {
                EventHandlers.Remove(eventType);
            }
        }
    }

    /// <summary>
    /// 注销指定实例的所有事件处理程序并从注册实例列表中移除
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterInstance(object targetObject)
    {
        if (targetObject is null)
        {
            if (OS.IsDebugBuild())
            {
                Logger.LogInfo("目标对象为 null，无法取消订阅。");
            }
            return;
        }

        lock (InstanceLock)
        {
            RegisteredInstances.Remove(targetObject);
        }

        UnregisterAllEventsForObject(targetObject);
    }

    /// <summary>
    /// 注销所有事件处理程序并清理所有注册信息
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnregisterAllEvents()
    {
        lock (EventHandlers)
        {
            EventHandlers.Clear();
        }
        
        lock (InstanceLock)
        {
            RegisteredInstances.Clear();
        }
        
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo("所有事件订阅已被注销。");
        }
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

        lock (InstanceLock)
        {
            if (RegisteredInstances.Contains(target))
            {
                return;
            }

            EventBusRegHelper.RegisterEventHandlers(target);
            RegisteredInstances.Add(target);
        }
    }

    /// <summary>
    /// 检查指定实例是否已在EventBus中注册
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInstanceRegistered(object target)
    {
        if (target == null) return false;
        
        lock (InstanceLock)
        {
            return RegisteredInstances.Contains(target);
        }
    }

    /// <summary>
    /// 获取当前已注册的实例数量
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRegisteredInstanceCount()
    {
        lock (InstanceLock)
        {
            return RegisteredInstances.Count;
        }
    }

    /// <summary>
    /// 获取当前已注册的事件类型数量
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetRegisteredEventTypeCount()
    {
        lock (EventHandlers)
        {
            return EventHandlers.Count;
        }
    }

    /// <summary>
    /// 获取指定事件类型的详细调试信息
    /// </summary>
    public static string GetEventTypeDebugInfo<TEvent>() where TEvent : EventBase
    {
        var eventType = typeof(TEvent);
        
        lock (EventHandlers)
        {
            if (EventHandlers.TryGetValue(eventType, out var collection))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"事件类型: {eventType.FullName}");
                sb.AppendLine($"处理程序数量: {collection.Count}");
                sb.AppendLine("处理程序详情 (按优先级排序):");
                sb.Append(collection.GetDebugInfo());
                return sb.ToString();
            }
            else
            {
                return $"事件类型 {eventType.FullName} 未注册任何处理程序";
            }
        }
    }
    
    /// <summary>
    /// 获取所有已注册事件类型的统计信息
    /// </summary>
    public static Dictionary<Type, int> GetEventStatistics()
    {
        lock (EventHandlers)
        {
            return EventHandlers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
        }
    }
}

/// <summary>
/// 事件订阅特性 - 支持优先级和接收已取消事件
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class EventSubscribeAttribute : Attribute
{
    /// <summary>
    /// 获取或设置处理程序优先级
    /// </summary>
    public EventPriority Priority { get; set; } = EventPriority.NORMAL;
    
    /// <summary>
    /// 是否接收已取消的事件
    /// </summary>
    public bool ReceiveCanceled { get; set; } = false;

    /// <summary>
    /// 传统的数字优先级支持（向后兼容）
    /// </summary>
    public int NumericPriority { get; set; } = 0;

    /// <summary>
    /// 默认构造函数 - 使用默认优先级
    /// </summary>
    public EventSubscribeAttribute()
    {
        Priority = EventPriority.NORMAL;
        ReceiveCanceled = false;
        NumericPriority = 0;
    }

    /// <summary>
    /// 使用EventPriority的构造函数
    /// </summary>
    public EventSubscribeAttribute(EventPriority priority, bool receiveCanceled = false)
    {
        Priority = priority;
        ReceiveCanceled = receiveCanceled;
        NumericPriority = 0;
    }

    /// <summary>
    /// 向后兼容的数字优先级构造函数
    /// </summary>
    public EventSubscribeAttribute(int priority)
    {
        NumericPriority = priority;
        Priority = ConvertToEventPriority(priority);
        ReceiveCanceled = false;
    }

    private static EventPriority ConvertToEventPriority(int numericPriority)
    {
        return numericPriority switch
        {
            >= 100 => EventPriority.HIGHEST,
            >= 50 => EventPriority.HIGH,
            > 0 => EventPriority.NORMAL,
            >= -50 => EventPriority.LOW,
            _ => EventPriority.LOWEST
        };
    }
}

/// <summary>
/// 自动注册事件处理程序特性
/// </summary>
/// <remarks>
/// <para>标记在类上的特性，表示该类支持EventBus自动注册</para>
/// <para>只有标记了此特性的类，其实例才会被自动注册系统处理</para>
/// <para>该特性本身不执行任何逻辑，仅用作标识符</para>
/// <para>通常与EventSubscribe特性配合使用</para>
/// </remarks>
/// <example>
/// <code>
/// [EventBusSubscriber]
/// public partial class GameManager : Node
/// {
///     [EventSubscribe(Priority = 100)]
///     public void OnGameStart(GameStartEvent evt)
///     {
///         InitializeGame();
///     }
///     
///     [EventSubscribe]
///     public async Task OnGameEnd(GameEndEvent evt)
///     {
///         await SaveProgress();
///         ShowMainMenu();
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public class EventBusSubscriberAttribute : Attribute
{
}