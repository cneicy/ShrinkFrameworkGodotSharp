using CommonSDK.Logger;
using Godot;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8603 // 可能返回 null 引用。

namespace CommonSDK.Utils;

/// <summary>
/// 查找工具类
/// <para>提供在场景树中查找节点的各种方法</para>
/// </summary>
public static class FindUtils
{
    private static readonly LogHelper Logger = new("FindUtils");
    private static SceneTree GetSceneTree()
    {
        return Engine.GetMainLoop() as SceneTree;
    }

    private static Node GetRoot()
    {
        return GetSceneTree()?.Root;
    }

    #region FindObjectOfType 系列方法

    /// <summary>
    /// 查找场景树中第一个指定类型的对象
    /// </summary>
    /// <typeparam name="T">要查找的对象类型</typeparam>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的对象，如果没有则返回null</returns>
    public static T FindObjectOfType<T>(bool includeInactive = false) where T : Node
    {
        var root = GetRoot();
        return root == null ? null : FindObjectOfTypeRecursive<T>(root, includeInactive);
    }

    /// <summary>
    /// 查找场景树中第一个指定类型的对象
    /// </summary>
    /// <param name="type">要查找的对象类型</param>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的对象，如果没有则返回null</returns>
    public static Node FindObjectOfType(Type type, bool includeInactive = false)
    {
        var root = GetRoot();
        return root == null ? null : FindObjectOfTypeRecursive(root, type, includeInactive);
    }

    /// <summary>
    /// 查找场景树中所有指定类型的对象
    /// </summary>
    /// <typeparam name="T">要查找的对象类型</typeparam>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的对象数组</returns>
    public static T[] FindObjectsOfType<T>(bool includeInactive = false) where T : Node
    {
        var root = GetRoot();
        if (root == null) return new T[0];

        var results = new List<T>();
        FindObjectsOfTypeRecursive<T>(root, results, includeInactive);
        return results.ToArray();
    }

    /// <summary>
    /// 查找场景树中所有指定类型的对象
    /// </summary>
    /// <param name="type">要查找的对象类型</param>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的对象数组</returns>
    public static Node[] FindObjectsOfType(Type type, bool includeInactive = false)
    {
        var root = GetRoot();
        if (root == null) return new Node[0];

        var results = new List<Node>();
        FindObjectsOfTypeRecursive(root, type, results, includeInactive);
        return results.ToArray();
    }

    #endregion

    #region Find 系列方法

    /// <summary>
    /// 根据名称查找第一个节点
    /// </summary>
    /// <param name="name">要查找的节点名称</param>
    /// <param name="exactMatch">是否进行精确匹配</param>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的节点，如果没有则返回null</returns>
    public static Node Find(string name, bool exactMatch = true, bool includeInactive = false)
    {
        var root = GetRoot();
        return root == null ? null : FindByNameRecursive(root, name, exactMatch, includeInactive);
    }

    /// <summary>
    /// 根据名称查找所有节点
    /// </summary>
    /// <param name="name">要查找的节点名称</param>
    /// <param name="exactMatch">是否进行精确匹配</param>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的节点数组</returns>
    public static Node[] FindAll(string name, bool exactMatch = true, bool includeInactive = false)
    {
        var root = GetRoot();
        if (root == null) return new Node[0];

        var results = new List<Node>();
        FindAllByNameRecursive(root, name, exactMatch, includeInactive, results);
        return results.ToArray();
    }

    /// <summary>
    /// 根据路径查找节点
    /// </summary>
    /// <param name="path">节点路径</param>
    /// <returns>找到的节点，如果没有则返回null</returns>
    public static Node FindByPath(string path)
    {
        var root = GetRoot();
        return root?.GetNode(path);
    }

    /// <summary>
    /// 根据路径查找指定类型的节点
    /// </summary>
    /// <typeparam name="T">要查找的节点类型</typeparam>
    /// <param name="path">节点路径</param>
    /// <returns>找到的节点，如果没有则返回null</returns>
    public static T FindByPath<T>(string path) where T : Node
    {
        return FindByPath(path) as T;
    }

    #endregion

    #region FindWithGroup 系列方法

    /// <summary>
    /// 根据组名查找第一个节点
    /// </summary>
    /// <param name="group">组名</param>
    /// <returns>找到的节点，如果没有则返回null</returns>
    public static Node FindWithGroup(string group)
    {
        var sceneTree = GetSceneTree();
        return sceneTree.GetFirstNodeInGroup(group);
    }

    /// <summary>
    /// 根据组名查找第一个指定类型的节点
    /// </summary>
    /// <typeparam name="T">要查找的节点类型</typeparam>
    /// <param name="group">组名</param>
    /// <returns>找到的节点，如果没有则返回null</returns>
    public static T FindWithGroup<T>(string group) where T : Node
    {
        return FindWithGroup(group) as T;
    }

    /// <summary>
    /// 根据组名查找所有节点
    /// </summary>
    /// <param name="group">组名</param>
    /// <returns>找到的节点数组</returns>
    public static Node[] FindObjectsWithGroup(string group)
    {
        var sceneTree = GetSceneTree();
        var nodes = sceneTree?.GetNodesInGroup(group);
        return nodes?.Cast<Node>().ToArray() ?? new Node[0];
    }

    /// <summary>
    /// 根据组名查找所有指定类型的节点
    /// </summary>
    /// <typeparam name="T">要查找的节点类型</typeparam>
    /// <param name="group">组名</param>
    /// <returns>找到的节点数组</returns>
    public static T[] FindObjectsWithGroup<T>(string group) where T : Node
    {
        var nodes = FindObjectsWithGroup(group);
        return nodes.OfType<T>().ToArray();
    }

    #endregion

    #region FindInChildren 系列方法

    // 此系列方法会先从传入的父节点找起

    /// <summary>
    /// 在父节点的子节点中查找第一个指定类型的节点
    /// </summary>
    /// <typeparam name="T">要查找的节点类型</typeparam>
    /// <param name="parent">父节点</param>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的节点，如果没有则返回null</returns>
    public static T FindInChildren<T>(Node parent, bool includeInactive = false) where T : Node
    {
        return parent == null ? null : FindObjectOfTypeRecursive<T>(parent, includeInactive);
    }

    /// <summary>
    /// 在父节点的子节点中查找所有指定类型的节点
    /// </summary>
    /// <typeparam name="T">要查找的节点类型</typeparam>
    /// <param name="parent">父节点</param>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的节点数组</returns>
    public static T[] FindAllInChildren<T>(Node parent, bool includeInactive = false) where T : Node
    {
        if (parent == null) return new T[0];

        var results = new List<T>();
        FindObjectsOfTypeRecursive(parent, results, includeInactive);
        return results.ToArray();
    }

    /// <summary>
    /// 在父节点的子节点中查找第一个指定名称的节点
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="name">要查找的节点名称</param>
    /// <param name="exactMatch">是否进行精确匹配</param>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>找到的节点，如果没有则返回null</returns>
    public static Node FindInChildren(Node parent, string name, bool exactMatch = true, bool includeInactive = false)
    {
        return parent == null ? null : FindByNameRecursive(parent, name, exactMatch, includeInactive);
    }

    #endregion

    #region 递归查找辅助方法

    private static T FindObjectOfTypeRecursive<T>(Node node, bool includeInactive) where T : Node
    {
        if (!includeInactive && !IsNodeActive(node))
        {
            return null;
        }

        if (node is T directResult)
        {
            return directResult;
        }

        if (HasScriptOfType<T>(node))
        {
            return node as T;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindObjectOfTypeRecursive<T>(child, includeInactive);
            if (found != null) return found;
        }

        return null;
    }

    private static Node FindObjectOfTypeRecursive(Node node, Type type, bool includeInactive)
    {
        if (!includeInactive && !IsNodeActive(node))
        {
            return null;
        }

        if (type.IsInstanceOfType(node) || HasScriptOfType(node, type))
        {
            return node;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindObjectOfTypeRecursive(child, type, includeInactive);
            if (found != null) return found;
        }

        return null;
    }

    private static void FindObjectsOfTypeRecursive<T>(Node node, List<T> results, bool includeInactive) where T : Node
    {
        if (!includeInactive && !IsNodeActive(node))
        {
            return;
        }

        if (node is T directResult)
        {
            results.Add(directResult);
        }
        else if (HasScriptOfType<T>(node))
        {
            if (node is T scriptResult)
            {
                results.Add(scriptResult);
            }
        }

        foreach (var child in node.GetChildren())
        {
            FindObjectsOfTypeRecursive<T>(child, results, includeInactive);
        }
    }

    private static void FindObjectsOfTypeRecursive(Node node, Type type, List<Node> results, bool includeInactive)
    {
        if (!includeInactive && !IsNodeActive(node))
        {
            return;
        }

        if (type.IsInstanceOfType(node) || HasScriptOfType(node, type))
        {
            results.Add(node);
        }

        foreach (var child in node.GetChildren())
        {
            FindObjectsOfTypeRecursive(child, type, results, includeInactive);
        }
    }

    private static Node FindByNameRecursive(Node node, string name, bool exactMatch, bool includeInactive)
    {
        var nodeName = node.Name.ToString();
        var nameMatches = exactMatch
            ? string.Equals(nodeName, name, StringComparison.OrdinalIgnoreCase)
            : nodeName.Contains(name, StringComparison.OrdinalIgnoreCase);

        if (nameMatches && (includeInactive || IsNodeActive(node)))
        {
            return node;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindByNameRecursive(child, name, exactMatch, includeInactive);
            if (found != null) return found;
        }

        return null;
    }

    private static void FindAllByNameRecursive(Node node, string name, bool exactMatch, bool includeInactive,
        List<Node> results)
    {
        var nodeName = node.Name.ToString();
        var nameMatches = exactMatch
            ? string.Equals(nodeName, name, StringComparison.OrdinalIgnoreCase)
            : nodeName.Contains(name, StringComparison.OrdinalIgnoreCase);

        if (nameMatches && (includeInactive || IsNodeActive(node)))
        {
            results.Add(node);
        }

        foreach (var child in node.GetChildren())
        {
            FindAllByNameRecursive(child, name, exactMatch, includeInactive, results);
        }
    }

    private static bool IsNodeActive(Node node)
    {
        if (node.ProcessMode == Node.ProcessModeEnum.Disabled)
            return false;

        return node switch
        {
            CanvasItem { Visible: false } or Node3D { Visible: false } => false,
            _ => true
        };
    }

    private static bool HasScriptOfType<T>(Node node) where T : Node
    {
        try
        {
            var nodeType = node.GetType();
            var targetType = typeof(T);
            if (targetType.IsAssignableFrom(nodeType))
            {
                return true;
            }

            var script = node.GetScript();
            if (script.VariantType != Variant.Type.Nil)
            {
                if (script.AsGodotObject() is Script scriptResource)
                {
                    var scriptPath = scriptResource.ResourcePath;
                    if (!string.IsNullOrEmpty(scriptPath))
                    {
                        var fileName = Path.GetFileNameWithoutExtension(scriptPath);
                        if (fileName == targetType.Name)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"检查脚本类型时出错: {ex.Message}");
        }

        return false;
    }

    private static string GetScriptTypeName(CSharpScript script)
    {
        try
        {
            var path = script.ResourcePath;
            if (!string.IsNullOrEmpty(path))
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                return fileName;
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    private static bool IsScriptDerivedFrom<T>(Node node, CSharpScript script) where T : Node
    {
        try
        {
            var targetType = typeof(T);
            var nodeType = node.GetType();

            return targetType.IsAssignableFrom(nodeType);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasScriptOfType(Node node, Type targetType)
    {
        try
        {
            var nodeType = node.GetType();

            if (targetType.IsAssignableFrom(nodeType))
            {
                return true;
            }

            var script = node.GetScript();
            if (script.VariantType != Variant.Type.Nil)
            {
                if (script.AsGodotObject() is Script scriptResource)
                {
                    var scriptPath = scriptResource.ResourcePath;
                    if (!string.IsNullOrEmpty(scriptPath))
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(scriptPath);
                        if (fileName == targetType.Name)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"检查脚本类型时出错: {ex.Message}");
        }

        return false;
    }

    #endregion

    #region 便捷方法

    /// <summary>
    /// 获取所有激活的节点
    /// </summary>
    /// <returns>激活的节点数组</returns>
    public static Node[] GetAllActiveNodes()
    {
        return FindObjectsOfType<Node>(includeInactive: false);
    }

    /// <summary>
    /// 获取所有节点
    /// </summary>
    /// <returns>节点数组</returns>
    public static Node[] GetAllNodes()
    {
        return FindObjectsOfType<Node>(includeInactive: true);
    }

    /// <summary>
    /// 计算指定类型的对象数量
    /// </summary>
    /// <typeparam name="T">要计算的对象类型</typeparam>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>对象数量</returns>
    public static int CountObjectsOfType<T>(bool includeInactive = false) where T : Node
    {
        return FindObjectsOfType<T>(includeInactive).Length;
    }

    /// <summary>
    /// 检查场景树中是否存在指定类型的对象
    /// </summary>
    /// <typeparam name="T">要检查的对象类型</typeparam>
    /// <param name="includeInactive">是否包含未激活的节点</param>
    /// <returns>如果存在则返回true，否则返回false</returns>
    public static bool HasObjectOfType<T>(bool includeInactive = false) where T : Node
    {
        return FindObjectOfType<T>(includeInactive) != null;
    }

    #endregion
}