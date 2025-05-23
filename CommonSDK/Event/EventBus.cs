using System.Reflection;
using Godot;


namespace CommonSDK.Event;

public partial class EventBus : Singleton<EventBus>
{
    private readonly Dictionary<string, Delegate> _eventHandlers = new();

    public void RegisterEvent<T>(string eventName, Func<T, object> handler)
    {
        if (!_eventHandlers.TryAdd(eventName, handler))
            _eventHandlers[eventName] = Delegate.Combine(_eventHandlers[eventName], handler);
    }

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
                        GD.PrintErr($"执行事件 {eventName} 时发生异常: {ex}");
                    }
                }
            }
            else
            {
                if (OS.IsDebugBuild())
                {
                    GD.PrintErr(
                        $"事件 {eventName} 的处理程序类型不匹配。预期 Func<{typeof(T).Name}, object>，实际为 {handler.GetType().Name}");
                }
            }
        }

        return results;
    }


    public void CancelEvent(string eventName)
    {
        if (!_eventHandlers.Remove(eventName)) return;
        if (OS.IsDebugBuild())
        {
            GD.Print($"事件 {eventName} 已被取消 (所有处理程序已移除)。");
        }
    }

    public void UnregisterAllEventsForObject(object targetObject)
    {
        if (targetObject is null)
        {
            if (OS.IsDebugBuild())
            {
                GD.Print("目标对象为 null，无法取消订阅。");
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
            GD.Print($"已为 {targetObject} 注销所有相关事件订阅。");
        }
    }

    public void UnregisterAllEvents()
    {
        _eventHandlers.Clear();
        if (OS.IsDebugBuild())
        {
            GD.Print("所有事件订阅已被注销。");
        }
    }

    public void RegisterEventHandlersFromAttributes(object target)
    {
        if (target == null)
        {
            if (OS.IsDebugBuild()) GD.PrintErr("RegisterEventHandlersFromAttributes: 目标对象为 null。");
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
                        GD.PrintErr(
                            $"方法 {method.Name} 在类 {target.GetType().Name} 中带有 EventSubscribeAttribute，但参数数量不为1。");
                    continue;
                }

                if (method.ReturnType != typeof(object))
                {
                    if (OS.IsDebugBuild())
                        GD.PrintErr(
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

                    if (OS.IsDebugBuild()) GD.Print($"自动注册事件: {eventName} -> {target.GetType().Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    if (OS.IsDebugBuild()) GD.PrintErr($"为事件 {eventName} 创建委托给方法 {method.Name} 失败: {ex}");
                }
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public class EventSubscribeAttribute : Attribute
{
    public EventSubscribeAttribute(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("Event name cannot be null or whitespace.", nameof(eventName));
        }

        EventName = eventName;
    }

    public string EventName { get; }
}