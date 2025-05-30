using CommonSDK.Logger;

namespace CommonSDK.BehaviorTree;

/// <summary>
    /// 行为树管理器单例 - 用于全局行为树管理
    /// </summary>
    public partial class BehaviorTreeManager : Singleton<BehaviorTreeManager>
    {
        private Dictionary<string, BehaviorTree> _behaviorTrees = new();
        private readonly LogHelper _logger = new("BehaviorTreeManager");
        protected override void OnSingletonReady()
        {
            _logger.LogInfo("行为树管理器已初始化");
        }
        
        protected override void OnSingletonDestroy()
        {
            _behaviorTrees.Clear();
            _logger.LogInfo("行为树管理器已销毁");
        }
        
        /// <summary>
        /// 注册行为树
        /// </summary>
        /// <param name="name">行为树名称</param>
        /// <param name="behaviorTree">行为树实例</param>
        public void RegisterBehaviorTree(string name, BehaviorTree behaviorTree)
        {
            _behaviorTrees[name] = behaviorTree;
            _logger.LogInfo($"注册行为树: {name}");
        }
        
        /// <summary>
        /// 获取行为树
        /// </summary>
        /// <param name="name">行为树名称</param>
        /// <returns>行为树实例</returns>
        public BehaviorTree GetBehaviorTree(string name)
        {
            return _behaviorTrees.TryGetValue(name, out BehaviorTree bt) ? bt : null;
        }
        
        /// <summary>
        /// 移除行为树
        /// </summary>
        /// <param name="name">行为树名称</param>
        public void UnregisterBehaviorTree(string name)
        {
            if (_behaviorTrees.Remove(name))
            {
                _logger.LogInfo($"移除行为树: {name}");
            }
        }
        
        /// <summary>
        /// 获取所有已注册的行为树名称
        /// </summary>
        /// <returns>行为树名称数组</returns>
        public string[] GetRegisteredBehaviorTrees()
        {
            return _behaviorTrees.Keys.ToArray();
        }
        
        /// <summary>
        /// 暂停所有行为树
        /// </summary>
        public void PauseAllTrees()
        {
            foreach (var tree in _behaviorTrees.Values)
            {
                tree.Pause();
            }
            _logger.LogInfo("暂停所有行为树");
        }
        
        /// <summary>
        /// 恢复所有行为树
        /// </summary>
        public void ResumeAllTrees()
        {
            foreach (var tree in _behaviorTrees.Values)
            {
                tree.Resume();
            }
            _logger.LogInfo("恢复所有行为树");
        }
        
        /// <summary>
        /// 重置所有行为树
        /// </summary>
        public void ResetAllTrees()
        {
            foreach (var tree in _behaviorTrees.Values)
            {
                tree.ResetTree();
            }
            _logger.LogInfo("重置所有行为树");
        }
    }