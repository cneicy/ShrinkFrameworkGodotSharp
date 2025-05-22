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
            GD.PrintErr($"[单例] {typeof(T).Name}创建失败: 场景树不可用");
            return;
        }
        
        var existingInstance = sceneTree.GetFirstNodeInGroup($"singleton_{typeof(T).Name}");
        
        if (existingInstance is T existing)
        {
            _instance = existing;
            return;
        }
        
        _instance = new T();
        
        var root = sceneTree.Root;
        //todo 暂时还有bug
        root.CallDeferred("add_child", _instance);
        
        _instance.AddToGroup($"singleton_{typeof(T).Name}");
        
        _instance.ProcessMode = ProcessModeEnum.Always;

        GD.Print($"[单例] {typeof(T).Name} 实例已经创建并添加到场景树");
    }
    
    public override void _Ready()
    {
        if (_instance == null)
        {
            _instance = (T)this;
            AddToGroup($"singleton_{typeof(T).Name}");
            ProcessMode = Node.ProcessModeEnum.Always;
            GD.Print($"[单例] {typeof(T).Name} 已经从场景实例化");
        }
        else if (_instance != this)
        {
            GD.PrintErr($"[单例] {typeof(T).Name} 实例已存在， 正在销毁");
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
        GD.Print($"[单例] {typeof(T).Name} 实例已销毁");
    }
    
    protected virtual void OnSingletonReady()
    {
    }
    
    protected virtual void OnSingletonDestroy()
    {
    }
    
    public static void DestroySingleton()
    {
        if (_instance != null)
        {
            _instance.QueueFree();
        }
    }
    
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _isQuitting = true;
        }
    }
}