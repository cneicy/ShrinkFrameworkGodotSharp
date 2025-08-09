using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CommonSDK.Logger;
using CommonSDK.ModGateway;
using Godot;

namespace ShrinkFrameworkGodotSharp;

/// <summary>
/// 🎯 Godot 线程安全任务管理器实现
/// </summary>
public partial class GodotThreadSafeTaskManager : Node, IThreadSafeTaskManager
{
    private static readonly ConcurrentDictionary<int, TaskCompletionSource<object>> PendingTasks = new();
    private static readonly ConcurrentQueue<(int taskId, Action action)> ActionQueue = new();
    private static readonly ConcurrentQueue<(int taskId, Func<object> func)> FunctionQueue = new();
    private static int _taskIdCounter;
    private static readonly LogHelper Logger = new("TaskManager");

    public override void _Ready()
    {
        Logger.LogInfo("🎯 Godot 任务管理器已就绪");
    }

    /// <summary>
    /// 每帧处理待执行的任务
    /// </summary>
    public override void _Process(double delta)
    {
        // 处理 Action 队列
        while (ActionQueue.TryDequeue(out var actionItem))
        {
            ExecuteActionOnMainThread(actionItem.taskId, actionItem.action);
        }

        // 处理 Function 队列
        while (FunctionQueue.TryDequeue(out var funcItem))
        {
            ExecuteFunctionOnMainThread(funcItem.taskId, funcItem.func);
        }
    }

    /// <summary>
    /// 在主线程异步执行操作
    /// </summary>
    public async Task CallDeferredAsync(Action action)
    {
        var taskCompletionSource = new TaskCompletionSource<object>();
        var taskId = CreateTask(taskCompletionSource);

        // 🎯 将任务加入队列，在主线程的 _Process 中执行
        ActionQueue.Enqueue((taskId, action));
        
        await taskCompletionSource.Task;
    }

    /// <summary>
    /// 在主线程异步执行操作并返回结果
    /// </summary>
    public async Task<TResult> CallDeferredAsync<TResult>(Func<TResult> func)
    {
        var taskCompletionSource = new TaskCompletionSource<object>();
        var taskId = CreateTask(taskCompletionSource);

        // 🎯 包装 Func<TResult> 为 Func<object>
        Func<object> wrappedFunc = () => func();
        FunctionQueue.Enqueue((taskId, wrappedFunc));
        
        var result = await taskCompletionSource.Task;
        return (TResult)result;
    }

    /// <summary>
    /// 🎯 在主线程执行 Action
    /// </summary>
    private void ExecuteActionOnMainThread(int taskId, Action action)
    {
        try
        {
            action.Invoke();
            CompleteTask(taskId, null);
        }
        catch (Exception ex)
        {
            Logger.LogError($"执行主线程Action异常: {ex.Message}");
            CompleteTaskWithError(taskId, ex);
        }
    }

    /// <summary>
    /// 🎯 在主线程执行 Func
    /// </summary>
    private void ExecuteFunctionOnMainThread(int taskId, Func<object> func)
    {
        try
        {
            var result = func.Invoke();
            CompleteTask(taskId, result);
        }
        catch (Exception ex)
        {
            Logger.LogError($"执行主线程Function异常: {ex.Message}");
            CompleteTaskWithError(taskId, ex);
        }
    }

    /// <summary>
    /// 创建新的任务ID并注册TaskCompletionSource
    /// </summary>
    private static int CreateTask(TaskCompletionSource<object> tcs)
    {
        var taskId = Interlocked.Increment(ref _taskIdCounter);
        PendingTasks[taskId] = tcs;
        return taskId;
    }

    /// <summary>
    /// 完成任务并清理
    /// </summary>
    private static void CompleteTask(int taskId, object result)
    {
        if (PendingTasks.TryRemove(taskId, out var tcs))
        {
            tcs.SetResult(result);
        }
    }

    /// <summary>
    /// 完成任务并设置异常
    /// </summary>
    private static void CompleteTaskWithError(int taskId, Exception exception)
    {
        if (PendingTasks.TryRemove(taskId, out var tcs))
        {
            tcs.SetException(exception);
        }
    }
}