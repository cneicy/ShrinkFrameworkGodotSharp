using System.Reflection;
using CommonSDK.Logger;
using Godot;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract


namespace CommonSDK.Event;

/// <summary>
/// 事件总线系统
/// <para>提供基于事件名称的发布-订阅模式实现</para>
/// <para>支持类型安全的事件处理和自动注册基于特性的事件处理程序</para>
/// </summary>
public partial class EventBus : Singleton<EventBus>
{
    /// <summary>
    /// 存储所有已注册的事件处理程序
    /// </summary>
    private readonly Dictionary<string, Delegate> _eventHandlers = new();

    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly LogHelper _logger = new LogHelper("EventBus");

    /// <summary>
    /// 注册事件处理程序
    /// <para>如果事件已存在处理程序，则将新处理程序与现有处理程序组合</para>
    /// </summary>
    /// <typeparam name="T">事件参数类型</typeparam>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理程序</param>
    public void RegisterEvent<T>(string eventName, Func<T, object> handler)
    {
        if (!_eventHandlers.TryAdd(eventName, handler))
            _eventHandlers[eventName] = Delegate.Combine(_eventHandlers[eventName], handler);
    }

    /// <summary>
    /// 注销事件处理程序
    /// <para>如果移除后没有处理程序，则完全移除该事件</para>
    /// </summary>
    /// <typeparam name="T">事件参数类型</typeparam>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">要注销的事件处理程序</param>
    public void UnregisterEvent<T>(string eventName, Func<T, object> handler)
    {
        if (!_eventHandlers.TryGetValue(eventName, out Delegate existingHandler)) return;
        var newHandler = Delegate.Remove(existingHandler, handler);
        if (newHandler == null || newHandler.GetInvocationList().Length == 0)
        {
            _eventHandlers.Remove(eventName);
        }
        else
        {
            _eventHandlers[eventName] = newHandler;
        }
    }

    /// <summary>
    /// 触发指定事件
    /// <para>调用所有注册到该事件的处理程序，并收集返回结果</para>
    /// <para>如果处理程序执行过程中出现异常，将被捕获并记录（仅在调试模式）</para>
    /// </summary>
    /// <typeparam name="T">事件参数类型</typeparam>
    /// <param name="eventName">事件名称</param>
    /// <param name="args">事件参数</param>
    /// <returns>所有处理程序的返回值列表</returns>
    public List<object> TriggerEvent<T>(string eventName, T args)
    {
        if (!_eventHandlers.TryGetValue(eventName, out var eventHandlerDelegate))
            return [];

        var results = new List<object>();
        if (eventHandlerDelegate == null) return results;
        foreach (var handler in eventHandlerDelegate.GetInvocationList())
        {
            if (handler is Func<T, object> typedHandler)
            {
                try
                {
                    results.Add(typedHandler(args));
                }
                catch (Exception ex)
                {
                    if (OS.IsDebugBuild())
                    {
                        _logger.LogError($"执行事件 {eventName} 时发生异常: {ex}");
                    }
                }
            }
            else
            {
                if (OS.IsDebugBuild())
                {
                    _logger.LogError(
                        $"事件 {eventName} 的处理程序类型不匹配。预期 Func<{typeof(T).Name}, object>，实际为 {handler.GetType().Name}");
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 取消指定事件（移除所有处理程序）
    /// </summary>
    /// <param name="eventName">要取消的事件名称</param>
    public void CancelEvent(string eventName)
    {
        if (!_eventHandlers.Remove(eventName)) return;
        if (OS.IsDebugBuild())
        {
            _logger.LogInfo($"事件 {eventName} 已被取消 (所有处理程序已移除)。");
        }
    }

    /// <summary>
    /// 注销指定对象的所有事件处理程序
    /// <para>用于在对象销毁前清理其所有事件订阅</para>
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    public void UnregisterAllEventsForObject(object targetObject)
    {
        if (targetObject is null)
        {
            if (OS.IsDebugBuild())
            {
                _logger.LogInfo("目标对象为 null，无法取消订阅。");
            }

            return;
        }

        var eventNames = new List<string>(_eventHandlers.Keys); // 复制键以避免在迭代时修改字典

        foreach (var eventName in eventNames)
        {
            if (!_eventHandlers.TryGetValue(eventName, out var eventHandlerDelegate) ||
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
                _eventHandlers.Remove(eventName);
            }
            else
            {
                _eventHandlers[eventName] = newDelegate;
            }
        }

        if (OS.IsDebugBuild())
        {
            _logger.LogInfo($"已为 {targetObject} 注销所有相关事件订阅。");
        }
    }

    /// <summary>
    /// 注销所有事件处理程序
    /// <para>完全清空事件总线</para>
    /// </summary>
    public void UnregisterAllEvents()
    {
        _eventHandlers.Clear();
        if (OS.IsDebugBuild())
        {
            _logger.LogInfo("所有事件订阅已被注销。");
        }
    }

    /// <summary>
    /// 从目标对象中自动注册带有特性标记的事件处理程序
    /// <para>扫描对象的所有方法，查找带有 EventSubscribeAttribute 特性的方法</para>
    /// <para>符合条件的方法必须有一个参数且返回类型为 object</para>
    /// </summary>
    /// <param name="target">包含事件处理程序的目标对象</param>
    public void RegisterEventHandlersFromAttributes(object target)
    {
        if (target == null)
        {
            if (OS.IsDebugBuild()) _logger.LogError("RegisterEventHandlersFromAttributes: 目标对象为 null。");
            return;
        }

        var methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        foreach (var method in methods)
        {
            var attributes = method.GetCustomAttributes(typeof(EventSubscribeAttribute), false);
            foreach (var attribute in attributes)
            {
                if (attribute is not EventSubscribeAttribute eventSubscribe) continue;

                var eventName = eventSubscribe.EventName;
                var parameters = method.GetParameters();

                if (parameters.Length != 1)
                {
                    if (OS.IsDebugBuild())
                        _logger.LogError(
                            $"方法 {method.Name} 在类 {target.GetType().Name} 中带有 EventSubscribeAttribute，但参数数量不为1。");
                    continue;
                }

                if (method.ReturnType != typeof(object))
                {
                    if (OS.IsDebugBuild())
                        _logger.LogError(
                            $"方法 {method.Name} 在类 {target.GetType().Name} 中带有 EventSubscribeAttribute，但返回类型不是 object。");
                    continue;
                }

                try
                {
                    var delegateTarget = method.IsStatic ? null : target;
                    var handlerDelegate = Delegate.CreateDelegate(
                        typeof(Func<,>).MakeGenericType(parameters[0].ParameterType, method.ReturnType),
                        delegateTarget,
                        method);

                    if (!_eventHandlers.TryAdd(eventName, handlerDelegate))
                    {
                        _eventHandlers[eventName] = Delegate.Combine(_eventHandlers[eventName], handlerDelegate);
                    }

                    if (OS.IsDebugBuild())
                        _logger.LogInfo($"自动注册事件: {eventName} -> {target.GetType().Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    if (OS.IsDebugBuild()) _logger.LogError($"为事件 {eventName} 创建委托给方法 {method.Name} 失败: {ex}");
                }
            }
        }
    }
}

/// <summary>
/// 事件订阅特性
/// <para>用于标记方法作为特定事件的处理程序</para>
/// <para>被标记的方法必须有一个参数且返回类型为 object</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class EventSubscribeAttribute : Attribute
{
    /// <summary>
    /// 创建新的事件订阅特性
    /// </summary>
    /// <param name="eventName">要订阅的事件名称</param>
    /// <exception cref="ArgumentException">当事件名称为空或仅包含空格时抛出</exception>
    public EventSubscribeAttribute(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("事件名不能为空或空格", nameof(eventName));
        }

        EventName = eventName;
    }

    /// <summary>
    /// 获取事件名称
    /// </summary>
    public string EventName { get; }
}