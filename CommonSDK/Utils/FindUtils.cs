using Godot;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8603 // 可能返回 null 引用。

namespace CommonSDK.Utils;

public static class FindUtils
{
    private static SceneTree GetSceneTree()
    {
        return Engine.GetMainLoop() as SceneTree;
    }

    private static Node GetRoot()
    {
        return GetSceneTree()?.Root;
    }

    #region FindObjectOfType 系列方法
    
    public static T FindObjectOfType<T>(bool includeInactive = false) where T : Node
    {
        var root = GetRoot();
        return root == null ? null : FindObjectOfTypeRecursive<T>(root, includeInactive);
    }
    
    public static Node FindObjectOfType(Type type, bool includeInactive = false)
    {
        var root = GetRoot();
        return root == null ? null : FindObjectOfTypeRecursive(root, type, includeInactive);
    }
    
    public static T[] FindObjectsOfType<T>(bool includeInactive = false) where T : Node
    {
        var root = GetRoot();
        if (root == null) return new T[0];

        var results = new List<T>();
        FindObjectsOfTypeRecursive<T>(root, results, includeInactive);
        return results.ToArray();
    }
    
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
    
    public static Node Find(string name, bool exactMatch = true, bool includeInactive = false)
    {
        var root = GetRoot();
        return root == null ? null : FindByNameRecursive(root, name, exactMatch, includeInactive);
    }
    
    public static Node[] FindAll(string name, bool exactMatch = true, bool includeInactive = false)
    {
        var root = GetRoot();
        if (root == null) return new Node[0];

        var results = new List<Node>();
        FindAllByNameRecursive(root, name, exactMatch, includeInactive, results);
        return results.ToArray();
    }
    
    public static Node FindByPath(string path)
    {
        var root = GetRoot();
        return root?.GetNode(path);
    }
    
    public static T FindByPath<T>(string path) where T : Node
    {
        return FindByPath(path) as T;
    }

    #endregion

    #region FindWithGroup 系列方法
    
    public static Node FindWithGroup(string group)
    {
        var sceneTree = GetSceneTree();
        return sceneTree.GetFirstNodeInGroup(group);
    }
    
    public static T FindWithGroup<T>(string group) where T : Node
    {
        return FindWithGroup(group) as T;
    }
    
    public static Node[] FindObjectsWithGroup(string group)
    {
        var sceneTree = GetSceneTree();
        var nodes = sceneTree?.GetNodesInGroup(group);
        return nodes?.Cast<Node>().ToArray() ?? new Node[0];
    }
    
    public static T[] FindObjectsWithGroup<T>(string group) where T : Node
    {
        var nodes = FindObjectsWithGroup(group);
        return nodes.OfType<T>().ToArray();
    }

    #endregion

    #region FindInChildren 系列方法
    // 此系列方法会先从传入的父节点找起

    public static T FindInChildren<T>(Node parent, bool includeInactive = false) where T : Node
    {
        return parent == null ? null : FindObjectOfTypeRecursive<T>(parent, includeInactive);
    }
    
    
    public static T[] FindAllInChildren<T>(Node parent, bool includeInactive = false) where T : Node
    {
        if (parent == null) return new T[0];

        var results = new List<T>();
        FindObjectsOfTypeRecursive(parent, results, includeInactive);
        return results.ToArray();
    }
    
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
        var nameMatches = exactMatch ? 
            string.Equals(nodeName, name, StringComparison.OrdinalIgnoreCase) : 
            nodeName.Contains(name, StringComparison.OrdinalIgnoreCase);

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

    private static void FindAllByNameRecursive(Node node, string name, bool exactMatch, bool includeInactive, List<Node> results)
    {
        var nodeName = node.Name.ToString();
        var nameMatches = exactMatch ? 
            string.Equals(nodeName, name, StringComparison.OrdinalIgnoreCase) : 
            nodeName.Contains(name, StringComparison.OrdinalIgnoreCase);

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
            GD.PrintErr($"检查脚本类型时出错: {ex.Message}");
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
            GD.PrintErr($"检查脚本类型时出错: {ex.Message}");
        }

        return false;
    }

    #endregion

    #region 便捷方法
    
    public static Node[] GetAllActiveNodes()
    {
        return FindObjectsOfType<Node>(includeInactive: false);
    }
    
    public static Node[] GetAllNodes()
    {
        return FindObjectsOfType<Node>(includeInactive: true);
    }
    
    public static int CountObjectsOfType<T>(bool includeInactive = false) where T : Node
    {
        return FindObjectsOfType<T>(includeInactive).Length;
    }
    
    public static bool HasObjectOfType<T>(bool includeInactive = false) where T : Node
    {
        return FindObjectOfType<T>(includeInactive) != null;
    }

    #endregion
}