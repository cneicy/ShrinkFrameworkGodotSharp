using CommonSDK.Logger;
using CommonSDK.Utils;
using Godot;

// ReSharper disable StaticMemberInGenericType
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8603 // 可能返回 null 引用。

namespace CommonSDK;

public abstract partial class Singleton<T> : Node where T : Singleton<T>, new()
{
    private static T _instance;
    private static readonly object Lock = new();
    private static bool _isQuitting;
    private static readonly LogHelper LogHelper = new($"Singleton Of {typeof(T).Name}");
    public static T Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Lock)
            {
                if (_instance == null && !_isQuitting)
                {
                    CreateInstance();
                }
            }

            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    private static void CreateInstance()
    {
        var mainLoop = Engine.GetMainLoop();
        if (mainLoop is not SceneTree sceneTree)
        {
            LogHelper.LogError($"{typeof(T).Name}创建失败: 场景树不可用");
            return;
        }

        var existingInstance = FindUtils.FindObjectOfType<T>();
        if (existingInstance != null)
        {
            _instance = existingInstance;
            LogHelper.LogInfo($"{typeof(T).Name} 找到现有实例，路径为{_instance.GetPath()}");
            return;
        }

        var groupInstance = FindUtils.FindWithGroup<T>($"singleton_{typeof(T).Name}");
        if (groupInstance != null)
        {
            _instance = groupInstance;
            LogHelper.LogInfo($"{typeof(T).Name} 通过组找到现有实例，路径为{_instance.GetPath()}");
            return;
        }

        _instance = CreateNewInstance();

        if (_instance == null)
        {
            LogHelper.LogError($"{typeof(T).Name} 创建失败");
            return;
        }

        var root = sceneTree.Root.GetNode("/root");
        root.CallDeferred(Node.MethodName.AddChild, _instance);

        _instance.AddToGroup($"singleton_{typeof(T).Name}");
        _instance.ProcessMode = ProcessModeEnum.Always;

        _instance.CallDeferred(MethodName.PrintInstancePath);
    }

    private static T CreateNewInstance()
    {
        var scenePath = $"res://singletons/{typeof(T).Name}.tscn";
        if (ResourceLoader.Exists(scenePath))
        {
            var scene = GD.Load<PackedScene>(scenePath);
            if (scene.Instantiate() is T instance)
            {
                LogHelper.LogInfo($"{typeof(T).Name} 从场景文件创建");
                return instance;
            }
        }

        var scriptPath = $"res://scripts/{typeof(T).Name}.cs";
        if (ResourceLoader.Exists(scriptPath))
        {
            var script = GD.Load<Script>(scriptPath);
            var node = new Node();
            node.SetScript(script);
            var instance = node as T;
            if (instance != null)
            {
                LogHelper.LogInfo($"{typeof(T).Name} 通过脚本创建");
                return instance;
            }
        }

        LogHelper.LogInfo($"{typeof(T).Name} 直接创建（可能缺少脚本）");
        return new T();
    }

    private void PrintInstancePath()
    {
        LogHelper.LogInfo($"{typeof(T).Name} 实例已经创建并添加到场景树，路径为{GetPath()}");
    }

    public override void _Ready()
    {
        if (_instance == null)
        {
            _instance = (T)this;
            AddToGroup($"singleton_{typeof(T).Name}");
            ProcessMode = ProcessModeEnum.Always;
            LogHelper.LogInfo($"{typeof(T).Name} 已经从场景实例化");
        }
        else if (_instance != this)
        {
            LogHelper.LogError($"{typeof(T).Name} 实例已存在，正在销毁");
            QueueFree();
            return;
        }

        OnSingletonReady();
    }

    public override void _ExitTree()
    {
        if (_instance != this) return;
        OnSingletonDestroy();
        _isQuitting = true;
        _instance = null;
        LogHelper.LogInfo($"{typeof(T).Name} 实例已销毁");
    }

    protected virtual void OnSingletonReady()
    {
    }

    protected virtual void OnSingletonDestroy()
    {
    }

    public static void DestroySingleton()
    {
        _instance.QueueFree();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _isQuitting = true;
        }
    }
}