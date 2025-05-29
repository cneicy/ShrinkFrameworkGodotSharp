using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using CommonSDK.Logger;
using CommonSDK.ModGateway;
using Godot;

namespace ShrinkFrameworkGodotSharp;

/// <summary>
/// 模组加载器类
/// <para>负责加载和管理Godot引擎中的模组，包括PCK文件、场景文件和模组DLL</para>
/// </summary>
public partial class ModLoader : Node
{
    private static readonly List<IMod> ModInstances = [];
    private static readonly Dictionary<string, ModMetadata> ModMetadata = new();
    private static readonly LogHelper Logger = new("ModLoader");

    /// <summary>
    /// 每帧处理方法
    /// </summary>
    /// <param name="delta">帧时间间隔</param>
    public override void _Process(double delta)
    {
        base._Process(delta);
        foreach (var instance in ModInstances)
        {
            instance.Loop();
        }
    }

    /// <summary>
    /// 节点准备就绪时调用
    /// <para>初始化模组加载器并开始加载模组</para>
    /// </summary>
    public override void _Ready()
    {
        Logger.LogInfo("正在开始加载模组");
        LoadMods();
    }

    /// <summary>
    /// 加载所有模组
    /// <para>从用户数据目录下的mods文件夹加载模组资源</para>
    /// </summary>
    private void LoadMods()
    {
        var modsDir = OS.GetUserDataDir() + "/mods/";
        Logger.LogInfo($"模组文件夹路径: {modsDir}");

        if (!Directory.Exists(modsDir))
        {
            Logger.LogInfo($"创建模组目录: {modsDir}");
            Directory.CreateDirectory(modsDir);
            return;
        }

        LoadPckFiles(modsDir);

        var mods = LoadModMetadata(modsDir);
        mods.Sort((a, b) => a.LoadOrder.CompareTo(b.LoadOrder));

        foreach (var mod in mods)
        {
            LoadModAssembly(mod);
        }
    }

    /// <summary>
    /// 加载PCK文件
    /// </summary>
    /// <param name="modsDir">模组目录路径</param>
    private static void LoadPckFiles(string modsDir)
    {
        var pckFiles = Directory.GetFiles(modsDir, "*.pck");
        Logger.LogInfo($"找到PCK文件: {pckFiles.Length}");

        foreach (var pckFile in pckFiles)
        {
            try
            {
                if (ProjectSettings.LoadResourcePack(pckFile))
                {
                    Logger.LogInfo($"已加载PCK: {Path.GetFileName(pckFile)}");
                }
                else
                {
                    Logger.LogError($"加载PCK失败: {pckFile}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"加载PCK异常: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 加载模组元数据
    /// </summary>
    /// <param name="modsDir">模组目录路径</param>
    /// <returns>模组元数据列表</returns>
    private static List<ModMetadata> LoadModMetadata(string modsDir)
    {
        var mods = new List<ModMetadata>();
        var modDirs = Directory.GetDirectories(modsDir);

        foreach (var modDir in modDirs)
        {
            var metadataPath = Path.Combine(modDir, "mod.json");
            if (!File.Exists(metadataPath)) continue;

            try
            {
                var json = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<ModMetadata>(json);

                if (string.IsNullOrEmpty(metadata.Id))
                {
                    Logger.LogError($"无效的模组元数据: {metadataPath} - 缺少ID");
                    continue;
                }

                metadata.Directory = modDir;
                ModMetadata[metadata.Id] = metadata;
                mods.Add(metadata);

                Logger.LogInfo($"已加载元数据: {metadata.Id} v{metadata.Version}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"解析元数据失败: {metadataPath} - {ex.Message}");
            }
        }

        return mods;
    }

    /// <summary>
    /// 加载模组程序集
    /// </summary>
    /// <param name="metadata">模组元数据</param>
    private void LoadModAssembly(ModMetadata metadata)
    {
        var dllFiles = Directory.GetFiles(metadata.Directory, "*.dll");
        if (dllFiles.Length == 0)
        {
            Logger.LogInfo($"模组无DLL: {metadata.Id}");
            return;
        }

        Logger.LogInfo($"加载模组: {metadata.Id}");

        LoadModScenes(metadata);

        foreach (var dllFile in dllFiles)
        {
            try
            {
                Logger.LogInfo($"加载模组库: {Path.GetFileName(dllFile)}");

                var alc = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
                if (alc == null) continue;

                var assembly = alc.LoadFromAssemblyPath(dllFile);
                ProcessAssemblyTypes(assembly, metadata);
            }
            catch (Exception ex)
            {
                Logger.LogError($"加载DLL失败: {dllFile} - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 加载模组场景文件
    /// </summary>
    /// <param name="metadata">模组元数据</param>
    private void LoadModScenes(ModMetadata metadata)
    {
        var sceneDir = Path.Combine(metadata.Directory, "scenes");
        if (!Directory.Exists(sceneDir)) return;

        var sceneFiles = Directory.GetFiles(sceneDir, "*.tscn");
        Logger.LogInfo($"找到场景文件: {sceneFiles.Length}");

        foreach (var sceneFile in sceneFiles)
        {
            try
            {
                var scenePath = $"res://{Path.GetRelativePath(OS.GetUserDataDir(), sceneFile)}";
                var scene = GD.Load<PackedScene>(scenePath);
                var instance = scene.Instantiate();

                GetTree().Root.AddChild(instance);
                Logger.LogInfo($"已加载场景: {Path.GetFileName(sceneFile)}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"加载场景失败: {sceneFile} - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 处理程序集中的类型
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <param name="metadata">模组元数据</param>
    private void ProcessAssemblyTypes(Assembly assembly, ModMetadata metadata)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!IsModClass(type)) continue;

            var baseType = type.BaseType;
            var instanceProp = baseType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

            if (instanceProp == null)
            {
                Logger.LogError($"找不到Instance属性: {type.FullName}");
                continue;
            }

            if (instanceProp.GetValue(null) is not IMod modInstance)
            {
                Logger.LogError($"实例化失败: {type.FullName}");
                continue;
            }

            SetModMetadata(modInstance, metadata);

            ModInstances.Add(modInstance);
            modInstance.Init();
            Logger.LogInfo($"成功加载模组: {type.FullName}");
        }
    }

    /// <summary>
    /// 设置模组元数据
    /// </summary>
    /// <param name="modInstance">模组实例</param>
    /// <param name="metadata">模组元数据</param>
    private static void SetModMetadata(IMod modInstance, ModMetadata metadata)
    {
        var modType = modInstance.GetType();

        var modIdProp = modType.GetProperty("ModId");
        var versionProp = modType.GetProperty("Version");
        var authorProp = modType.GetProperty("Author");
        var descriptionProp = modType.GetProperty("Description");

        modIdProp?.SetValue(modInstance, metadata.Id);
        versionProp?.SetValue(modInstance, metadata.Version);
        authorProp?.SetValue(modInstance, metadata.Author);
        descriptionProp?.SetValue(modInstance, metadata.Description);
    }

    /// <summary>
    /// 检查类型是否为模组类
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <returns>如果是模组类则返回true，否则返回false</returns>
    private static bool IsModClass(Type type)
    {
        if (type.IsAbstract || type.IsInterface) return false;
        return type.BaseType?.IsGenericType == true &&
               type.BaseType.GetGenericTypeDefinition() == typeof(ModBase<>);
    }
}