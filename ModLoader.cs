using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using CommonSDK.Logger;
using CommonSDK.ModGateway;
using Godot;

namespace ShrinkFrameworkGodotSharp;

public partial class ModLoader : Node
{
    private static readonly List<IMod> ModInstances = [];
    private static readonly LogHelper Logger = new("ModLoader");

    public override void _Process(double delta)
    {
        base._Process(delta);
        foreach (var instance in ModInstances)
        {
            instance.Loop();
        }
    }

    public override void _Ready()
    {
        Logger.LogInfo("正在开始加载模组");
        LoadMods();
    }

    private static void LoadMods()
    {
        var modsDir = OS.GetUserDataDir() + "/mods/";
        Logger.LogInfo("模组文件夹路径: " + modsDir);

        if (Directory.Exists(modsDir))
        {
            Logger.LogInfo("模组文件夹存在");
            var dllFiles = Directory.GetFiles(modsDir, "*.dll");
            Logger.LogInfo("找到库文件共: " + dllFiles.Length);

            foreach (var dllFile in dllFiles)
            {
                Logger.LogInfo("加载模组库文件: " + dllFile);

                var alc = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
                if (alc == null) continue;

                var assembly = alc.LoadFromAssemblyPath(dllFile);

                foreach (var type in assembly.GetTypes())
                {
                    if (!IsModClass(type)) continue;

                    var baseType = type.BaseType;
                    //获取实例以绕过static限制
                    //https://github.com/godotengine/godot/issues/75160#issuecomment-1478573429
                    var instanceProp = baseType?.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.Static
                    );

                    if (instanceProp == null)
                    {
                        Logger.LogError($"找不到Instance属性: {type.FullName}");
                        continue;
                    }

                    if (instanceProp.GetValue(null) is IMod modInstance)
                    {
                        ModInstances.Add(modInstance);
                        modInstance.Init();
                        Logger.LogInfo($"成功加载Mod: {type.FullName}");
                    }
                    else
                    {
                        Logger.LogError($"实例化失败: {type.FullName}");
                    }
                }
            }
        }
        else
        {
            Logger.LogInfo($"模组文件夹未找到: {modsDir}");
        }
    }

    private static bool IsModClass(Type type)
    {
        if (type.IsAbstract || type.IsInterface) return false;
        return type.BaseType?.IsGenericType == true &&
               type.BaseType.GetGenericTypeDefinition() == typeof(ModBase<>);
    }
}