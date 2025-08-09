using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;
using CommonSDK.Logger;
using CommonSDK.Mixin;
using CommonSDK.ModGateway;
using Godot;
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable HeuristicUnreachableCode

namespace ShrinkFrameworkGodotSharp;

/// <summary>
/// 模组加载器
/// <para>负责异步加载和管理Godot引擎中的模组，包括PCK文件、场景文件和模组DLL</para>
/// </summary>
public partial class ModLoader : Node
{
    private static readonly List<IMod> ModInstances = [];
    // ReSharper disable once CollectionNeverQueried.Local
    private static readonly Dictionary<string, ModMetadata> ModMetadata = new();
    private static readonly LogHelper Logger = new("ModLoader");

    private static readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> PendingTasks = new();
    private static int _taskIdCounter;

    public enum LoadingState
    {
        NotStarted,
        LoadingMods,
        InitializingMods,
        ApplyingMixins,
        StartingMods,
        Ready
    }
    
    private LoadingState _currentState = LoadingState.NotStarted;
    private bool _modsCanLoop;

    /// <summary>
    /// 每帧处理方法
    /// </summary>
    /// <param name="delta">帧时间间隔</param>
    public override void _Process(double delta)
    {
        base._Process(delta);
        
        if (!_modsCanLoop || _currentState != LoadingState.Ready) return;
        
        foreach (var instance in ModInstances)
        {
            try
            {
                instance.Loop(delta);
            }
            catch (Exception ex)
            {
                Logger.LogError($"模组 Loop 异常 {instance.GetType().Name}: {ex.Message}");
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        if (!_modsCanLoop || _currentState != LoadingState.Ready) return;
        
        foreach (var instance in ModInstances)
        {
            try
            {
                instance.PhysicsLoop(delta);
            }
            catch (Exception ex)
            {
                Logger.LogError($"模组 PhysicsLoop 异常 {instance.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 节点准备就绪时调用
    /// <para>异步初始化模组加载器并开始加载模组</para>
    /// </summary>
    public override void _Ready()
    {
        Logger.LogInfo("🚀 开始异步加载模组系统...");
        
        var taskManager = new GodotThreadSafeTaskManager();
        AddChild(taskManager);
        
        ModGateway.SetTaskManager(taskManager);
    
        // 启动异步加载流程
        _ = StartAsyncModLoading();
    }

    /// <summary>
    /// 🎯 异步模组加载主流程
    /// </summary>
    private async Task StartAsyncModLoading()
    {
        try
        {
            Logger.LogInfo("📋 步骤 1/5: 初始化 Mixin 处理器");
            MixinProcessor.Initialize();
            
            _currentState = LoadingState.LoadingMods;
            Logger.LogInfo("📦 步骤 2/5: 异步加载模组文件");
            await LoadModsAsync();
            
            _currentState = LoadingState.InitializingMods;
            Logger.LogInfo("⚙️ 步骤 3/5: 初始化模组");
            await InitializeModsAsync();
            
            _currentState = LoadingState.ApplyingMixins;
            Logger.LogInfo("🔧 步骤 4/5: 应用 Mixin 系统");
            await ApplyMixinsAsync();
            
            _currentState = LoadingState.StartingMods;
            Logger.LogInfo("🎯 步骤 5/5: 启动模组");
            await StartModsAsync();
            
            _currentState = LoadingState.Ready;
            _modsCanLoop = true;
            Logger.LogInfo("✅ 模组系统异步加载完成！开始运行 Loop 和 PhysicsLoop");
        }
        catch (Exception ex)
        {
            Logger.LogError($"❌ 异步加载模组时发生致命错误: {ex.Message}");
            Logger.LogError($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    #region 🎯 异步辅助方法

    /// <summary>
    /// 创建新的任务ID并注册TaskCompletionSource
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private static int CreateTask(TaskCompletionSource<bool> tcs)
    {
        var taskId = System.Threading.Interlocked.Increment(ref _taskIdCounter);
        PendingTasks[taskId] = tcs;
        return taskId;
    }

    /// <summary>
    /// 完成任务并清理
    /// </summary>
    private static void CompleteTask(int taskId, bool success)
    {
        if (PendingTasks.TryRemove(taskId, out var tcs))
        {
            tcs.SetResult(success);
        }
    }

    #endregion

    /// <summary>
    /// 🎯 异步加载所有模组
    /// </summary>
    private async Task LoadModsAsync()
    {
        var modsDir = Path.Combine(OS.GetUserDataDir(), "mods");
        Logger.LogInfo($"模组文件夹路径: {modsDir}");

        if (!Directory.Exists(modsDir))
        {
            Directory.CreateDirectory(modsDir);
            Logger.LogInfo("已创建模组文件夹");
            return;
        }

        await LoadPckFilesAsync(modsDir);

        var modFolders = Directory.GetDirectories(modsDir);
        var modMetadataList = new List<ModMetadata>();
    
        foreach (var folder in modFolders)
        {
            try
            {
                var metadata = LoadModMetadata(folder);
                if (metadata != null)
                {
                    modMetadataList.Add(metadata);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 加载模组元数据失败 {Path.GetFileName(folder)}: {ex.Message}");
            }
        }
        
        var sortedMods = modMetadataList
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Id)
            .ToList();

        Logger.LogInfo("🎯 模组加载顺序:");
        for (var i = 0; i < sortedMods.Count; i++)
        {
            var mod = sortedMods[i];
            Logger.LogInfo($"  {i + 1}. [{mod.LoadOrder}] {mod.Id} v{mod.Version}");
        }

        foreach (var metadata in sortedMods)
        {
            try
            {
                Logger.LogInfo($"📦 按优先级加载模组: {metadata.Id} (LoadOrder: {metadata.LoadOrder})");
                await LoadModAssemblyAsync(metadata);
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 加载模组失败 {metadata.Id}: {ex.Message}");
            }
        }

        Logger.LogInfo($"📦 异步加载完成: {ModInstances.Count} 个模组");
    }

    /// <summary>
    /// 🎯 异步加载PCK文件
    /// </summary>
    private async Task LoadPckFilesAsync(string modsDir)
    {
        var pckFiles = Directory.GetFiles(modsDir, "*.pck");
        Logger.LogInfo($"找到PCK文件: {pckFiles.Length}");

        var taskManager = ModGateway.GetTaskManager();
        if (taskManager == null)
        {
            Logger.LogError("TaskManager 未初始化，无法加载PCK文件");
            return;
        }

        foreach (var pckFile in pckFiles)
        {
            try
            {
                var fileName = Path.GetFileName(pckFile);
            
                var success = await taskManager.CallDeferredAsync(() => 
                    ProjectSettings.LoadResourcePack(pckFile));
                
                if (success)
                {
                    Logger.LogInfo($"✅ 已加载PCK: {fileName}");
                }
                else
                {
                    Logger.LogError($"❌ 加载PCK失败: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 加载PCK异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 🎯 在主线程加载 PCK
    /// </summary>
    private void LoadPckOnMainThread(string pckFile, int taskId)
    {
        try
        {
            var success = ProjectSettings.LoadResourcePack(pckFile);
            CompleteTask(taskId, success);
        }
        catch (Exception ex)
        {
            Logger.LogError($"主线程加载PCK异常: {ex.Message}");
            CompleteTask(taskId, false);
        }
    }


    /// <summary>
    /// 🎯 异步加载模组程序集
    /// </summary>
    private async Task LoadModAssemblyAsync(ModMetadata metadata)
    {
        var dllFiles = Directory.GetFiles(metadata.Directory, "*.dll");
        if (dllFiles.Length == 0)
        {
            Logger.LogInfo($"模组无DLL: {metadata.Id}");
            return;
        }
        Logger.LogInfo($"📚 加载模组: {metadata.Id}");
        await LoadModScenesAsync(metadata);

        var dllTasks = dllFiles.Select(async dllFile =>
        {
            try
            {
                Logger.LogInfo($"📖 加载模组库: {Path.GetFileName(dllFile)}");

                var alc = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
                if (alc == null) return;

                var assembly = await Task.Run(() => alc.LoadFromAssemblyPath(dllFile));
                await ProcessAssemblyTypesAsync(assembly, metadata);
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 加载DLL失败: {dllFile} - {ex.Message}");
            }
        });

        await Task.WhenAll(dllTasks);
    }

    /// <summary>
    /// 🎯 异步加载模组场景文件
    /// </summary>
    private async Task LoadModScenesAsync(ModMetadata metadata)
    {
        var sceneDir = Path.Combine(metadata.Directory, "scenes");
        if (!Directory.Exists(sceneDir)) return;

        var sceneFiles = Directory.GetFiles(sceneDir, "*.tscn");
        Logger.LogInfo($"找到场景文件: {sceneFiles.Length}");

        var taskManager = ModGateway.GetTaskManager();
        if (taskManager == null)
        {
            Logger.LogError("TaskManager 未初始化，无法加载场景文件");
            return;
        }
        
        foreach (var sceneFile in sceneFiles)
        {
            try
            {
                await taskManager.CallDeferredAsync(() =>
                {
                    var scenePath = $"res://{Path.GetRelativePath(OS.GetUserDataDir(), sceneFile)}";
                    var scene = GD.Load<PackedScene>(scenePath);
                    var instance = scene.Instantiate();
                    GetTree().Root.AddChild(instance);
                    Logger.LogInfo($"✅ 已加载场景: {Path.GetFileName(sceneFile)}");
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 加载场景失败: {sceneFile} - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 🎯 在主线程加载场景
    /// </summary>
    private void LoadSceneOnMainThread(string sceneFile, int taskId)
    {
        try
        {
            var scenePath = $"res://{Path.GetRelativePath(OS.GetUserDataDir(), sceneFile)}";
            var scene = GD.Load<PackedScene>(scenePath);
            var instance = scene.Instantiate();

            GetTree().Root.AddChild(instance);
            Logger.LogInfo($"✅ 已加载场景: {Path.GetFileName(sceneFile)}");
            
            CompleteTask(taskId, true);
        }
        catch (Exception ex)
        {
            Logger.LogError($"主线程加载场景异常: {ex.Message}");
            CompleteTask(taskId, false);
        }
    }

    /// <summary>
    /// 🎯 异步处理程序集中的类型
    /// </summary>
    private async Task ProcessAssemblyTypesAsync(Assembly assembly, ModMetadata metadata)
    {
        var types = await Task.Run(assembly.GetTypes);
        
        foreach (var type in types)
        {
            if (type.GetCustomAttribute<MixinAttribute>() != null)
            {
                var mixinAttr = type.GetCustomAttribute<MixinAttribute>();
                var targetType = mixinAttr!.TargetType;
                
                Logger.LogInfo($"🔧 发现 Mixin: {type.Name} -> {targetType.Name}");
                MixinProcessor.RegisterMixin(type, targetType);
                continue;
            }
            
            // 处理普通模组类
            if (!IsModClass(type)) continue;

            await CreateModInstanceAsync(type, metadata);
        }
    }

/// <summary>
/// 🎯 修复异步创建模组实例 - 添加详细调试
/// </summary>
/// <summary>
/// 🎯 异步创建模组实例 - 修复歧义
/// </summary>
private async Task CreateModInstanceAsync(Type type, ModMetadata metadata)
{
    try
    {
        Logger.LogInfo($"🔧 正在创建模组实例: {type.FullName}");
        Logger.LogInfo($"   - 基类: {type.BaseType?.FullName}");
        
        var baseType = type.BaseType;
        while (baseType != null && !baseType.Name.StartsWith("ModBase"))
        {
            baseType = baseType.BaseType;
        }

        if (baseType == null)
        {
            Logger.LogError($"❌ 未找到 ModBase 基类: {type.FullName}");
            return;
        }

        var instanceProp = baseType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProp == null)
        {
            Logger.LogError($"❌ 找不到Instance属性: {type.FullName}");
            return;
        }

        Logger.LogInfo($"🎯 找到Instance属性类型: {instanceProp.PropertyType.FullName}");

        var taskManager = ModGateway.GetTaskManager();
        if (taskManager == null)
        {
            Logger.LogError("❌ TaskManager 未初始化");
            return;
        }

        var modInstance = await taskManager.CallDeferredAsync(() => 
        {
            try 
            {
                Logger.LogInfo("⚙️ 正在获取单例实例...");
                var instance = instanceProp.GetValue(null);
                var instanceTypeName = instance?.GetType().FullName ?? "null";
                Logger.LogInfo($"✅ 成功获取实例: {instanceTypeName}");
                return instance as IMod;
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 获取单例实例异常: {ex.Message}");
                throw;
            }
        });

        if (modInstance == null)
        {
            Logger.LogError($"❌ 实例化失败或实例不是IMod类型: {type.FullName}");
            return;
        }

        Logger.LogInfo("🔧 开始设置模组元数据...");
        SetModMetadata(modInstance, metadata);
        
        ModInstances.Add(modInstance);
        
        await taskManager.CallDeferredAsync(() => 
        {
            if (modInstance is Node node)
            {
                AddChild(node);
                Logger.LogInfo($"✅ 已添加模组节点到场景树: {type.Name}");
            }
            else
            {
                Logger.LogWarn($"⚠️ 模组实例不是Node类型: {type.Name}");
            }
        });
        
        Logger.LogInfo($"✅ 成功创建模组实例: {type.FullName}");
    }
    catch (Exception ex)
    {
        Logger.LogError($"❌ 创建模组实例失败 {type.FullName}: {ex.Message}");
        Logger.LogError($"   堆栈跟踪: {ex.StackTrace}");
        
        if (ex is TargetInvocationException { InnerException: not null } tie)
        {
            Logger.LogError($"   内部异常: {tie.InnerException.Message}");
            Logger.LogError($"   内部堆栈: {tie.InnerException.StackTrace}");
        }
    }
}
    
    /// <summary>
    /// 🎯 加载单个模组的元数据
    /// </summary>
    private static ModMetadata LoadModMetadata(string modDir)
    {
        var metadataPath = Path.Combine(modDir, "mod.json");
        if (!File.Exists(metadataPath))
        {
            Logger.LogWarn($"⚠️ 未找到模组配置文件: {metadataPath}");
            return null;
        }

        try
        {
            var json = File.ReadAllText(metadataPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
        
            var metadata = JsonSerializer.Deserialize<ModMetadata>(json, options);

            if (metadata == null)
            {
                Logger.LogError($"❌ 反序列化失败: {metadataPath}");
                return null;
            }

            if (string.IsNullOrEmpty(metadata.Id) && string.IsNullOrEmpty(metadata.Name))
            {
                Logger.LogError($"❌ 无效的模组元数据: {metadataPath} - 缺少ID或Name");
                return null;
            }

            if (string.IsNullOrEmpty(metadata.Id))
            {
                metadata.Id = metadata.Name;
            }
            metadata.Directory = modDir;
            ModMetadata[metadata.Id] = metadata;

            return metadata;
        }
        catch (Exception ex)
        {
            Logger.LogError($"❌ 解析模组元数据失败: {metadataPath} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 🎯 在主线程添加模组节点
    /// </summary>
    private void AddModNodeOnMainThread(Node modNode, int taskId)
    {
        try
        {
            AddChild(modNode);
            CompleteTask(taskId, true);
        }
        catch (Exception ex)
        {
            Logger.LogError($"主线程添加节点异常: {ex.Message}");
            CompleteTask(taskId, false);
        }
    }

    /// <summary>
    /// 🎯 异步初始化模组
    /// </summary>
    private async Task InitializeModsAsync()
    {
        var initTasks = ModInstances.Select(async mod =>
        {
            try
            {
                Logger.LogInfo($"⚙️ 初始化模组: {mod.GetType().Name}");
                await mod.InitAsync();
                Logger.LogInfo($"✅ 模组初始化完成: {mod.GetType().Name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 模组初始化失败 {mod.GetType().Name}: {ex.Message}");
            }
        });

        await Task.WhenAll(initTasks);
        Logger.LogInfo($"✅ 所有模组初始化完成");
    }

    /// <summary>
    /// 🎯 异步应用 Mixin
    /// </summary>
    private async Task ApplyMixinsAsync()
    {
        try
        {
            Logger.LogInfo("🔧 开始应用 Mixin 系统...");
            await Task.Run(MixinProcessor.ApplyMixins);
            Logger.LogInfo("✅ Mixin 系统应用完成");
        }
        catch (Exception ex)
        {
            Logger.LogError($"❌ 应用 Mixin 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 🎯 异步启动模组
    /// </summary>
    private async Task StartModsAsync()
    {
        var startTasks = ModInstances.Select(async mod =>
        {
            try
            {
                Logger.LogInfo($"🎯 启动模组: {mod.GetType().Name}");
                await mod.StartAsync();
                Logger.LogInfo($"✅ 模组启动完成: {mod.GetType().Name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ 模组启动失败 {mod.GetType().Name}: {ex.Message}");
            }
        });

        await Task.WhenAll(startTasks);
        Logger.LogInfo($"✅ 所有模组启动完成");
    }

    /// <summary>
    /// 设置模组元数据
    /// </summary>
    private void SetModMetadata(IMod modInstance, ModMetadata metadata)
{
    try
    {
        var type = modInstance.GetType();
        
        var modIdProp = type.GetProperty("ModId");
        if (modIdProp != null && modIdProp.CanWrite)
        {
            var modIdValue = !string.IsNullOrEmpty(metadata.Id) ? metadata.Id : metadata.Name;
            modIdProp.SetValue(modInstance, modIdValue);
        }
        
        var versionProp = type.GetProperty("Version");
        if (versionProp != null && versionProp.CanWrite)
        {
            versionProp.SetValue(modInstance, metadata.Version);
        }
        
        var loadOrderProp = type.GetProperty("LoadOrder");
        if (loadOrderProp != null && loadOrderProp.CanWrite)
        {
            loadOrderProp.SetValue(modInstance, metadata.LoadOrder);
        }
        
        var authorsProp = type.GetProperty("Authors");
        if (authorsProp != null && authorsProp.CanWrite && authorsProp.PropertyType == typeof(string[]))
        {
            var authorsArray = metadata.Authors;
            authorsProp.SetValue(modInstance, authorsArray);
        }
        else
        {
            var authorProp = type.GetProperty("Author");
            if (authorProp != null && authorProp.CanWrite && authorProp.PropertyType == typeof(string))
            {
                var authorsArray = metadata.Authors;
                var authorString = authorsArray.Length > 0
                    ? string.Join(", ", (IEnumerable<string>)authorsArray)
                    : string.Empty;
                authorProp.SetValue(modInstance, authorString);
            }
        }
        
        var descProp = type.GetProperty("Description");
        if (descProp != null && descProp.CanWrite)
        {
            descProp.SetValue(modInstance, metadata.Description);
        }
        
        var metadataId = metadata.Id;
        var metadataVersion = metadata.Version;
        var metadataDescription = metadata.Description;
        var authorsForLog = metadata.Authors;
        var authorsDisplayForLog = string.Join(", ", (IEnumerable<string>)authorsForLog);

        Logger.LogInfo($"✅ 已设置模组元数据: {metadataId}");
        Logger.LogInfo($"   - 版本: {metadataVersion}");
        Logger.LogInfo($"   - 作者: [{authorsDisplayForLog}]");
        Logger.LogInfo($"   - 描述: {metadataDescription}");
    }
    catch (Exception ex)
    {
        var errorId = metadata?.Id ?? metadata?.Name ?? "未知模组";
        Logger.LogError($"❌ 设置模组元数据失败 {errorId}: {ex.Message}");
        Logger.LogError($"   堆栈跟踪: {ex.StackTrace}");
    }
}

    /// <summary>
    /// 检查类型是否为模组类
    /// </summary>
    private static bool IsModClass(Type type)
    {
        if (type.IsAbstract || type.IsInterface) return false;
        return type.BaseType?.IsGenericType == true &&
               type.BaseType.GetGenericTypeDefinition() == typeof(ModBase<>);
    }

    /// <summary>
    /// 🎯 获取当前加载状态（用于调试）
    /// </summary>
    private LoadingState GetCurrentState() => _currentState;

    /// <summary>
    /// 🎯 检查模组是否可以运行 Loop
    /// </summary>
    private bool CanModsLoop() => _modsCanLoop;
}