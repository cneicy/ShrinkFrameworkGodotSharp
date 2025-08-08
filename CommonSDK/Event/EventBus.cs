using System.Reflection;
using System.Runtime.CompilerServices;
using CommonSDK.Logger;
using Godot;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace CommonSDK.Event;

/// <summary>
/// 事件基类，所有自定义事件都应继承此类
/// </summary>
/// <remarks>
/// 这个抽象基类为所有事件提供了统一的类型约束，确保只有继承自EventBase的类型才能在EventBus中使用。
/// 通过继承此类，可以为事件系统提供类型安全和一致性保证。
/// </remarks>
public abstract class EventBase
{
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
/// <example>
/// 基本使用示例:
/// <code>
/// // 定义事件
/// public class PlayerHealthChangedEvent : EventBase
/// {
///     public int OldHealth { get; set; }
///     public int NewHealth { get; set; }
/// }
/// 
/// // 手动注册处理程序
/// EventBus.RegisterEvent&lt;PlayerHealthChangedEvent&gt;(evt =&gt; 
/// {
///     GD.Print($"Health changed: {evt.OldHealth} -&gt; {evt.NewHealth}");
/// }, priority: 10);
/// 
/// // 触发事件
/// await EventBus.TriggerEventAsync(new PlayerHealthChangedEvent 
/// { 
///     OldHealth = 100, 
///     NewHealth = 90 
/// });
/// </code>
/// </example>
public static class EventBus
{
    /// <summary>
    /// 存储所有已注册的事件处理程序（带优先级）
    /// </summary>
    /// <remarks>
    /// 使用字典结构按事件类型分类存储处理程序，每个事件类型对应一个优先级处理程序集合。
    /// 键为事件类型，值为PrioritizedHandlerCollection实例。
    /// </remarks>
    private static readonly Dictionary<Type, PrioritizedHandlerCollection> EventHandlers = new();

    /// <summary>
    /// 日志记录器实例
    /// </summary>
    /// <remarks>
    /// 用于记录EventBus系统的运行状态、错误信息和调试信息。
    /// 只在调试模式下输出详细日志，避免影响发布版本的性能。
    /// </remarks>
    public static readonly LogHelper Logger = new("EventBus");

    /// <summary>
    /// 存储已经注册过的实例，避免重复注册（实例级别）
    /// </summary>
    /// <remarks>
    /// 使用HashSet跟踪已注册的对象实例，防止同一个对象被重复注册多次。
    /// 这对于自动注册系统特别重要，可以避免重复订阅造成的性能问题。
    /// </remarks>
    private static readonly HashSet<object> RegisteredInstances = new();

    /// <summary>
    /// 实例注册操作的线程安全锁
    /// </summary>
    /// <remarks>
    /// 保证RegisteredInstances集合在多线程环境下的操作安全性。
    /// 防止并发注册/注销操作导致的数据竞争问题。
    /// </remarks>
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
    /// <remarks>
    /// 采用双重检查锁定模式，确保EventAutoRegHelper只被初始化一次。
    /// 这个方法在首次触发事件时被调用，保证自动注册系统的可用性。
    /// </remarks>
    private static void EnsureAutoManagerInitialized()
    {
        if (_hasEnsuredAutoRegInit) return;
        
        lock (AutoRegInitLock)
        {
            if (_hasEnsuredAutoRegInit) return;
            
            // 确保静态管理器初始化
            EventAutoRegHelper.EnsureInitialized();
            
            _hasEnsuredAutoRegInit = true;
        }
    }

    /// <summary>
    /// 静态构造函数，初始化时注册当前程序集中的静态事件处理程序
    /// </summary>
    /// <remarks>
    /// 在首次访问EventBus类型时自动执行，扫描并注册所有标记了EventSubscribe特性的静态方法。
    /// 这确保了静态事件处理程序在系统启动时就已经准备就绪。
    /// </remarks>
    static EventBus()
    {
        RegStaticEventHandler();
    }

    /// <summary>
    /// 注册静态事件处理程序
    /// </summary>
    /// <remarks>
    /// 委托给EventBusRegHelper执行实际的反射扫描和注册工作。
    /// 方法被标记为AggressiveInlining以优化性能。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RegStaticEventHandler()
    {
        EventBusRegHelper.RegStaticEventHandler();
    }

    /// <summary>
    /// 注册事件处理程序（异步版本）
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须继承自EventBase</typeparam>
    /// <param name="handler">异步事件处理程序</param>
    /// <param name="priority">处理程序优先级，数值越大优先级越高，默认为0</param>
    /// <remarks>
    /// 手动注册异步事件处理程序的主要入口点。
    /// 支持优先级设置，高优先级的处理程序会先执行。
    /// 线程安全，可以在任何时候调用。
    /// </remarks>
    /// <example>
    /// <code>
    /// EventBus.RegisterEvent&lt;PlayerDeathEvent&gt;(async evt =&gt; 
    /// {
    ///     await ShowDeathAnimation();
    ///     await SaveGameData();
    /// }, priority: 100);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Func<TEvent, Task> handler, int priority = 0) where TEvent : EventBase
    {
        RegisterEventInternal(typeof(TEvent), handler, priority, $"Manual Async Handler");
    }

    /// <summary>
    /// 注册事件处理程序（同步版本）
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须继承自EventBase</typeparam>
    /// <param name="handler">同步事件处理程序</param>
    /// <param name="priority">处理程序优先级，数值越大优先级越高，默认为0</param>
    /// <remarks>
    /// 手动注册同步事件处理程序的入口点。
    /// 内部会将同步处理程序包装为异步版本，以保持系统的一致性。
    /// 支持优先级设置，线程安全。
    /// </remarks>
    /// <example>
    /// <code>
    /// EventBus.RegisterEvent&lt;PlayerLevelUpEvent&gt;(evt =&gt; 
    /// {
    ///     GD.Print($"Player reached level {evt.NewLevel}!");
    ///     UpdateUI();
    /// }, priority: 50);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RegisterEvent<TEvent>(Action<TEvent> handler, int priority = 0) where TEvent : EventBase
    {
        Func<TEvent, Task> asyncHandler = arg =>
        {
            handler(arg);
            return Task.CompletedTask;
        };
        RegisterEventInternal(typeof(TEvent), asyncHandler, priority, $"Manual Sync Handler");
    }

    /// <summary>
    /// 内部事件注册方法
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理程序委托</param>
    /// <param name="priority">优先级</param>
    /// <param name="debugInfo">调试信息字符串</param>
    /// <remarks>
    /// 所有事件注册操作的核心实现方法。
    /// 负责创建或获取对应事件类型的处理程序集合，并添加新的处理程序。
    /// 线程安全，支持优先级排序。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegisterEventInternal(Type eventType, Delegate handler, int priority, string debugInfo = "")
    {
        lock (EventHandlers)
        {
            if (!EventHandlers.TryGetValue(eventType, out var collection))
            {
                collection = new PrioritizedHandlerCollection();
                EventHandlers[eventType] = collection;
                
                if (OS.IsDebugBuild())
                {
                    Logger.LogInfo($"创建新的事件处理程序集合: {eventType.FullName}");
                    Logger.LogInfo($"当前事件处理程序字典大小: {EventHandlers.Count}");
                }
            }

            collection.Add(handler, priority, debugInfo);

            if (OS.IsDebugBuild())
            {
                Logger.LogInfo($"成功注册事件: {eventType.FullName}, 优先级: {priority}, 异步: {handler.GetType().Name.Contains("Func")}");
            }
        }
    }

    /// <summary>
    /// 注销事件处理程序（异步版本）
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须继承自EventBase</typeparam>
    /// <param name="handler">要注销的异步事件处理程序</param>
    /// <remarks>
    /// 从指定事件类型的处理程序集合中移除指定的异步处理程序。
    /// 如果移除后集合为空，会自动清理整个事件类型的注册信息。
    /// 线程安全操作。
    /// </remarks>
    /// <example>
    /// <code>
    /// var handler = async (PlayerDeathEvent evt) => await ShowDeathScreen();
    /// EventBus.RegisterEvent(handler);
    /// // 稍后注销
    /// EventBus.UnregisterEvent(handler);
    /// </code>
    /// </example>
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
    /// <typeparam name="TEvent">事件类型，必须继承自EventBase</typeparam>
    /// <param name="handler">要注销的同步事件处理程序</param>
    /// <remarks>
    /// 从指定事件类型的处理程序集合中移除指定的同步处理程序。
    /// 由于同步处理程序在内部被包装为异步，需要特殊的匹配逻辑来找到对应的处理程序。
    /// 线程安全操作。
    /// </remarks>
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
                foreach (var h in handlers)
                {
                    if (IsWrappedSyncHandler(h, handler))
                    {
                        collection.Remove(h);
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
    /// <param name="asyncHandler">异步处理程序委托</param>
    /// <param name="targetHandler">目标同步处理程序委托</param>
    /// <returns>如果匹配则返回true，否则返回false</returns>
    /// <remarks>
    /// 用于匹配被包装为异步的同步处理程序。
    /// 通过反射检查委托的目标对象和方法信息来确定是否匹配。
    /// 这个方法对于正确注销同步处理程序至关重要。
    /// </remarks>
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
    /// 异步触发指定类型的事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须继承自EventBase</typeparam>
    /// <param name="eventArgs">事件参数实例</param>
    /// <returns>表示异步操作的Task</returns>
    /// <remarks>
    /// <para>按优先级顺序依次执行所有已注册的事件处理程序</para>
    /// <para>如果处理程序执行过程中发生异常，会记录错误但不会中断其他处理程序的执行</para>
    /// <para>在首次调用时会自动初始化自动注册管理器</para>
    /// <para>在调试模式下会输出详细的执行日志</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var healthEvent = new PlayerHealthChangedEvent 
    /// { 
    ///     OldHealth = 100, 
    ///     NewHealth = 90 
    /// };
    /// 
    /// await EventBus.TriggerEventAsync(healthEvent);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task TriggerEventAsync<TEvent>(TEvent eventArgs) where TEvent : EventBase
    {
        EnsureAutoManagerInitialized();
        var eventType = typeof(TEvent);
        
        if (OS.IsDebugBuild())
        {
            Logger.LogInfo($"准备触发事件: {eventType.FullName}");
            Logger.LogInfo($"当前已注册的事件类型数量: {EventHandlers.Count}");
            
            foreach (var kvp in EventHandlers)
            {
                Logger.LogInfo($"已注册事件: {kvp.Key.FullName}");
            }
        }
        
        PrioritizedHandlerCollection? collection;
        lock (EventHandlers)
        {
            if (!EventHandlers.TryGetValue(eventType, out collection))
            {
                if (OS.IsDebugBuild())
                {
                    Logger.LogWarn($"没有找到事件 {eventType.FullName} 的处理程序");
                }
                return;
            }
        }

        if (collection == null) 
        {
            if (OS.IsDebugBuild())
            {
                Logger.LogWarn($"事件 {eventType.Name} 的处理程序集合为 null");
            }
            return;
        }

        // 获取按优先级排序的处理程序
        var handlers = collection.GetSortedHandlers();
        
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
    /// 同步触发指定类型的事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须继承自EventBase</typeparam>
    /// <param name="eventArgs">事件参数实例</param>
    /// <remarks>
    /// <para>按优先级顺序依次执行所有已注册的事件处理程序</para>
    /// <para>对于异步处理程序，会阻塞等待其完成</para>
    /// <para>如果处理程序执行过程中发生异常，会记录错误但不会中断其他处理程序的执行</para>
    /// <para>建议优先使用异步版本以获得更好的性能</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var levelUpEvent = new PlayerLevelUpEvent { NewLevel = 5 };
    /// EventBus.TriggerEvent(levelUpEvent);
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TriggerEvent<TEvent>(TEvent eventArgs) where TEvent : EventBase
    {
        EnsureAutoManagerInitialized();
        var eventType = typeof(TEvent);
        
        PrioritizedHandlerCollection? collection;
        lock (EventHandlers)
        {
            if (!EventHandlers.TryGetValue(eventType, out collection))
                return;
        }

        if (collection == null) return;

        // 获取按优先级排序的处理程序
        var handlers = collection.GetSortedHandlers();

        foreach (var handler in handlers)
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
    /// 取消指定事件类型的所有处理程序
    /// </summary>
    /// <typeparam name="TEvent">要取消的事件类型</typeparam>
    /// <remarks>
    /// 移除指定事件类型的所有已注册处理程序。
    /// 这个操作是不可逆的，调用后该事件类型的所有处理程序都会被清除。
    /// 线程安全操作。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 清除所有PlayerHealthChangedEvent的处理程序
    /// EventBus.CancelEvent&lt;PlayerHealthChangedEvent&gt;();
    /// </code>
    /// </example>
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
    /// <param name="targetObject">要注销处理程序的目标对象</param>
    /// <remarks>
    /// 遍历所有已注册的事件类型，移除属于指定对象的处理程序。
    /// 如果某个事件类型的处理程序全部被移除，会自动清理该事件类型的注册信息。
    /// 这个方法通常在对象销毁时调用，防止内存泄漏。
    /// </remarks>
    /// <example>
    /// <code>
    /// var gameObject = new GameObject();
    /// // 注册一些事件处理程序...
    /// 
    /// // 在对象销毁时清理
    /// EventBus.UnregisterAllEventsForObject(gameObject);
    /// </code>
    /// </example>
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
    /// <param name="targetObject">要注销的目标实例</param>
    /// <remarks>
    /// 这个方法不仅会移除对象的所有事件处理程序，还会从RegisteredInstances集合中移除该对象。
    /// 这确保了对象的完全清理，防止内存泄漏和重复注册问题。
    /// 通常在对象生命周期结束时调用。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 在Node的_ExitTree方法中调用
    /// public override void _ExitTree()
    /// {
    ///     EventBus.UnregisterInstance(this);
    ///     base._ExitTree();
    /// }
    /// </code>
    /// </example>
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
    /// <remarks>
    /// 这是一个全局清理方法，会移除所有已注册的事件处理程序和实例信息。
    /// 通常只在应用程序关闭或需要完全重置事件系统时调用。
    /// 调用后需要重新注册所需的事件处理程序。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 在应用程序关闭时清理
    /// public override void _ExitTree()
    /// {
    ///     EventBus.UnregisterAllEvents();
    /// }
    /// </code>
    /// </example>
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
    /// <param name="target">要注册的目标对象</param>
    /// <remarks>
    /// <para>使用反射扫描目标对象的所有方法，注册带有EventSubscribe特性的方法为事件处理程序</para>
    /// <para>支持同步和异步方法的自动识别和包装</para>
    /// <para>防止重复注册同一个对象实例</para>
    /// <para>线程安全操作</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [EventBusSubscriber]
    /// public class GameLogic : Node
    /// {
    ///     public override void _Ready()
    ///     {
    ///         EventBus.AutoRegister(this);
    ///     }
    ///     
    ///     [EventSubscribe(Priority = 10)]
    ///     public void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
    ///     {
    ///         // 处理逻辑
    ///     }
    /// }
    /// </code>
    /// </example>
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
    /// <param name="target">要检查的目标对象</param>
    /// <returns>如果已注册返回true，否则返回false</returns>
    /// <remarks>
    /// 线程安全的查询方法，用于检查对象是否已经通过AutoRegister方法注册过。
    /// 这对于防止重复注册和调试非常有用。
    /// </remarks>
    /// <example>
    /// <code>
    /// if (!EventBus.IsInstanceRegistered(this))
    /// {
    ///     EventBus.AutoRegister(this);
    /// }
    /// </code>
    /// </example>
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
    /// <returns>已注册实例的总数</returns>
    /// <remarks>
    /// 返回通过AutoRegister方法注册的对象实例总数。
    /// 这个方法主要用于监控和调试目的，帮助了解系统的注册状态。
    /// 线程安全操作。
    /// </remarks>
    /// <example>
    /// <code>
    /// var instanceCount = EventBus.GetRegisteredInstanceCount();
    /// GD.Print($"当前已注册 {instanceCount} 个实例");
    /// </code>
    /// </example>
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
    /// <returns>已注册事件类型的总数</returns>
    /// <remarks>
    /// 返回EventBus中有处理程序的事件类型总数。
    /// 每个事件类型至少有一个处理程序时才会被计入。
    /// 线程安全操作。
    /// </remarks>
    /// <example>
    /// <code>
    /// var eventTypeCount = EventBus.GetRegisteredEventTypeCount();
    /// GD.Print($"系统中共有 {eventTypeCount} 种事件类型");
    /// </code>
    /// </example>
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
    /// <typeparam name="TEvent">要查询的事件类型</typeparam>
    /// <returns>包含事件类型详细信息的格式化字符串</returns>
    /// <remarks>
    /// <para>返回指定事件类型的完整信息，包括:</para>
    /// <list type="bullet">
    /// <item><description>事件类型的完整名称</description></item>
    /// <item><description>已注册的处理程序数量</description></item>
    /// <item><description>每个处理程序的优先级和调试信息</description></item>
    /// <item><description>按优先级排序的处理程序列表</description></item>
    /// </list>
    /// <para>这个方法主要用于调试和监控，帮助开发者了解事件处理的详细情况</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var debugInfo = EventBus.GetEventTypeDebugInfo&lt;PlayerHealthChangedEvent&gt;();
    /// GD.Print(debugInfo);
    /// // 输出示例:
    /// // 事件类型: MyGame.Events.PlayerHealthChangedEvent
    /// // 处理程序数量: 3
    /// // 处理程序详情 (按优先级排序):
    /// //   [1] Priority: 100, Info: Static GameLogic.OnHealthChanged (Sync, Priority: 100)
    /// //   [2] Priority: 50, Info: Instance UIManager.UpdateHealthBar (Async, Priority: 50)
    /// //   [3] Priority: 0, Info: Manual Async Handler
    /// </code>
    /// </example>
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
}

/// <summary>
/// 事件订阅特性
/// </summary>
/// <remarks>
/// <para>用于标记方法作为特定事件的处理程序</para>
/// <para>被标记的方法必须满足以下条件:</para>
/// <list type="bullet">
/// <item><description>有且仅有一个参数，且参数类型必须继承自EventBase</description></item>
/// <item><description>返回类型为void（同步）或Task（异步）</description></item>
/// <item><description>可以是静态方法或实例方法</description></item>
/// <item><description>可以是public、private或protected方法</description></item>
/// </list>
/// <para>支持优先级设置，数值越大优先级越高，相同优先级的处理程序执行顺序未定义</para>
/// </remarks>
/// <example>
/// <code>
/// [EventBusSubscriber]
/// public class GameManager : Node
/// {
///     // 高优先级异步处理程序
///     [EventSubscribe(Priority = 100)]
///     public async Task OnPlayerDeath(PlayerDeathEvent evt)
///     {
///         await SaveGameData();
///         await ShowGameOverScreen();
///     }
///     
///     // 默认优先级同步处理程序
///     [EventSubscribe]
///     public void OnPlayerLevelUp(PlayerLevelUpEvent evt)
///     {
///         UpdatePlayerStats(evt.NewLevel);
///     }
///     
///     // 低优先级静态处理程序
///     [EventSubscribe(Priority = -10)]
///     public static void OnGlobalEvent(GlobalEvent evt)
///     {
///         LogEvent(evt);
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class EventSubscribeAttribute : Attribute
{
    /// <summary>
    /// 获取或设置处理程序优先级
    /// </summary>
    /// <value>
    /// 优先级数值，数值越大优先级越高，默认为0
    /// </value>
    /// <remarks>
    /// <para>优先级决定了事件处理程序的执行顺序:</para>
    /// <list type="bullet">
    /// <item><description>正数: 高优先级，优先执行</description></item>
    /// <item><description>0: 默认优先级，正常执行</description></item>
    /// <item><description>负数: 低优先级，延后执行</description></item>
    /// </list>
    /// <para>建议的优先级范围: -100 到 100</para>
    /// <para>系统保留优先级: 1000以上为系统内部使用</para>
    /// </remarks>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// 初始化EventSubscribeAttribute的新实例
    /// </summary>
    /// <param name="priority">处理程序优先级，数值越大优先级越高，默认为0</param>
    /// <remarks>
    /// 创建事件订阅特性实例，可以通过构造函数或属性设置优先级。
    /// </remarks>
    /// <example>
    /// <code>
    /// // 使用构造函数设置优先级
    /// [EventSubscribe(50)]
    /// public void OnEvent(MyEvent evt) { }
    /// 
    /// // 使用属性设置优先级
    /// [EventSubscribe(Priority = 75)]
    /// public async Task OnEventAsync(MyEvent evt) { }
    /// </code>
    /// </example>
    public EventSubscribeAttribute(int priority = 0)
    {
        Priority = priority;
    }
}

/// <summary>
/// 带优先级的事件处理程序包装器
/// </summary>
/// <remarks>
/// 内部类，用于封装事件处理程序及其相关元数据。
/// 包含处理程序委托、优先级信息和调试信息。
/// </remarks>
internal class PrioritizedEventHandler
{
    /// <summary>
    /// 获取事件处理程序委托
    /// </summary>
    /// <value>实际的事件处理程序委托实例</value>
    public Delegate Handler { get; }
    
    /// <summary>
    /// 获取处理程序的优先级
    /// </summary>
    /// <value>优先级数值，数值越大优先级越高</value>
    public int Priority { get; }
    
    /// <summary>
    /// 获取调试信息字符串
    /// </summary>
    /// <value>用于调试和日志记录的描述性字符串</value>
    public string DebugInfo { get; }

    /// <summary>
    /// 初始化PrioritizedEventHandler的新实例
    /// </summary>
    /// <param name="handler">事件处理程序委托</param>
    /// <param name="priority">优先级</param>
    /// <param name="debugInfo">调试信息字符串</param>
    /// <remarks>
    /// 创建一个新的带优先级的事件处理程序包装器实例。
    /// </remarks>
    public PrioritizedEventHandler(Delegate handler, int priority, string debugInfo = "")
    {
        Handler = handler;
        Priority = priority;
        DebugInfo = debugInfo;
    }
}

/// <summary>
/// 优先级处理程序集合
/// </summary>
/// <remarks>
/// <para>管理同一事件类型的所有处理程序，支持按优先级排序</para>
/// <para>线程安全设计，支持并发访问</para>
/// <para>使用延迟排序策略，只有在获取排序结果时才执行排序操作</para>
/// <para>支持按目标对象批量移除处理程序</para>
/// </remarks>
internal class PrioritizedHandlerCollection
{
    /// <summary>
    /// 存储处理程序的内部列表
    /// </summary>
    private readonly List<PrioritizedEventHandler> _handlers = new();
    
    /// <summary>
    /// 线程安全锁对象
    /// </summary>
    private readonly object _lock = new();
    
    /// <summary>
    /// 标记是否需要重新排序
    /// </summary>
    private bool _needsSort = true;

    /// <summary>
    /// 添加新的事件处理程序
    /// </summary>
    /// <param name="handler">处理程序委托</param>
    /// <param name="priority">优先级</param>
    /// <param name="debugInfo">调试信息</param>
    /// <remarks>
    /// 线程安全地向集合中添加新的处理程序。
    /// 添加后会标记需要重新排序。
    /// </remarks>
    public void Add(Delegate handler, int priority, string debugInfo = "")
    {
        lock (_lock)
        {
            _handlers.Add(new PrioritizedEventHandler(handler, priority, debugInfo));
            _needsSort = true;
        }
    }

    /// <summary>
    /// 移除指定的事件处理程序
    /// </summary>
    /// <param name="handler">要移除的处理程序委托</param>
    /// <returns>如果成功移除返回true，否则返回false</returns>
    /// <remarks>
    /// 线程安全地从集合中移除指定的处理程序。
    /// 使用委托的Equals方法进行匹配。
    /// </remarks>
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

    /// <summary>
    /// 移除指定目标对象的所有处理程序
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <remarks>
    /// 线程安全地移除所有属于指定目标对象的处理程序。
    /// 通过比较处理程序的Target属性来匹配。
    /// 这个方法在对象销毁时特别有用。
    /// </remarks>
    public void RemoveTarget(object target)
    {
        lock (_lock)
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i].Handler.Target == target)
                {
                    _handlers.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// 获取按优先级排序的处理程序数组
    /// </summary>
    /// <returns>按优先级降序排列的处理程序委托数组</returns>
    /// <remarks>
    /// <para>返回按优先级排序的处理程序数组，优先级高的在前</para>
    /// <para>使用延迟排序策略，只有在需要时才执行排序操作</para>
    /// <para>排序结果会被缓存，直到下次添加处理程序</para>
    /// <para>线程安全操作</para>
    /// </remarks>
    public Delegate[] GetSortedHandlers()
    {
        lock (_lock)
        {
            if (_needsSort)
            {
                // 按优先级降序排列（优先级高的先执行）
                _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                _needsSort = false;
            }
            
            return _handlers.Select(h => h.Handler).ToArray();
        }
    }

    /// <summary>
    /// 获取集合中处理程序的数量
    /// </summary>
    /// <value>当前集合中处理程序的总数</value>
    /// <remarks>
    /// 线程安全地返回集合中处理程序的数量。
    /// </remarks>
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

    /// <summary>
    /// 清空集合中的所有处理程序
    /// </summary>
    /// <remarks>
    /// 线程安全地移除集合中的所有处理程序。
    /// 清空后会重置排序标志。
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            _handlers.Clear();
            _needsSort = false;
        }
    }

    /// <summary>
    /// 获取调试信息字符串
    /// </summary>
    /// <returns>包含所有处理程序详细信息的格式化字符串</returns>
    /// <remarks>
    /// 返回按优先级排序的处理程序列表的详细信息。
    /// 包含每个处理程序的优先级和调试信息。
    /// 主要用于调试和监控目的。
    /// </remarks>
    public string GetDebugInfo()
    {
        lock (_lock)
        {
            var sb = new System.Text.StringBuilder();
            var sortedHandlers = _handlers.OrderByDescending(h => h.Priority).ToArray();
            
            for (int i = 0; i < sortedHandlers.Length; i++)
            {
                var handler = sortedHandlers[i];
                sb.AppendLine($"  [{i + 1}] Priority: {handler.Priority}, Info: {handler.DebugInfo}");
            }
            
            return sb.ToString();
        }
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
