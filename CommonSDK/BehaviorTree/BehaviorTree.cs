using CommonSDK.Logger;
using Godot;

namespace CommonSDK.BehaviorTree;

// ========== 行为树管理器 ==========
    
    /// <summary>
    /// 行为树管理器
    /// </summary>
    public partial class BehaviorTree : Node
    {
        [Signal]
        public delegate void TreeCompletedEventHandler(NodeStatus status);
        
        [Export] public BehaviorNode RootNode;
        [Export] public bool AutoRun = true;
        [Export] public float TickRate = 0.1f; // 执行频率（秒）
        [Export] public bool DebugMode = false;
        
        private float tickTimer = 0f;
        private Blackboard _blackboard = new Blackboard();
        private NodeStatus _lastStatus = NodeStatus.Running;
        
        /// <summary>
        /// 黑板数据
        /// </summary>
        public Blackboard Blackboard => _blackboard;
        
        /// <summary>
        /// 日志帮助类实例
        /// </summary>
        public LogHelper Logger { get; private set; }
        
        public override void _Ready()
        {
            // 初始化日志帮助类
            Logger = new LogHelper($"BehaviorTree-{GetParent().Name}");
            
            if (RootNode == null)
            {
                var children = GetChildren().OfType<BehaviorNode>().ToArray();
                if (children.Length > 0)
                {
                    RootNode = children[0];
                }
            }
            
            InitializeTree();
        }
        
        public override void _Process(double delta)
        {
            if (!AutoRun || RootNode == null) return;
            
            tickTimer += (float)delta;
            
            if (tickTimer >= TickRate)
            {
                tickTimer = 0f;
                var status = Tick(delta);
                
                if (status != NodeStatus.Running && status != _lastStatus)
                {
                    EmitSignal(SignalName.TreeCompleted, (int)status);
                }
                
                _lastStatus = status;
            }
        }
        
        /// <summary>
        /// 初始化行为树
        /// </summary>
        private void InitializeTree()
        {
            if (RootNode != null)
            {
                SetupNode(RootNode);
                RootNode.Initialize();
                
                if (DebugMode)
                {
                    Logger.LogInfo("行为树初始化完成");
                }
            }
        }
        
        /// <summary>
        /// 设置节点的引用
        /// </summary>
        private void SetupNode(BehaviorNode node)
        {
            node.Blackboard = _blackboard;
            node.BehaviorTree = this;
            node.DebugMode = DebugMode;
            
            // 递归设置子节点
            foreach (BehaviorNode child in node.GetChildren())
            {
                SetupNode(child);
            }
        }
        
        /// <summary>
        /// 执行一次行为树
        /// </summary>
        /// <param name="delta">时间间隔</param>
        /// <returns>执行状态</returns>
        public NodeStatus Tick(double delta)
        {
            if (RootNode == null)
            {
                if (DebugMode)
                {
                    Logger.LogError("没有根节点");
                }
                return NodeStatus.Failure;
            }
            
            return RootNode.Execute(delta);
        }
        
        /// <summary>
        /// 重置行为树
        /// </summary>
        public void ResetTree()
        {
            RootNode?.Reset();
            _lastStatus = NodeStatus.Running;
            
            if (DebugMode)
            {
                Logger.LogInfo("行为树已重置");
            }
        }
        
        /// <summary>
        /// 暂停自动执行
        /// </summary>
        public void Pause()
        {
            AutoRun = false;
        }
        
        /// <summary>
        /// 恢复自动执行
        /// </summary>
        public void Resume()
        {
            AutoRun = true;
        }
    }