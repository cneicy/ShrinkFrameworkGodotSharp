# BehaviorTree - 行为树系统

一个为 Godot C# 项目设计的完整行为树系统，支持组合节点、装饰器节点、条件节点和动作节点，提供黑板数据共享和全局管理功能。

## ✨ 特性

- 🌳 **完整节点类型** - 支持序列、选择器、并行、装饰器等所有标准行为树节点
- 📋 **黑板系统** - 节点间数据共享和状态管理
- 🎮 **Godot集成** - 完美集成Godot节点系统和信号机制
- 🔧 **可视化编辑** - 支持在Godot编辑器中可视化构建行为树
- 🐛 **调试支持** - 内置调试日志和状态监控
- 🔄 **状态管理** - 支持暂停、重置和状态查询
- 🌍 **全局管理** - 单例管理器统一管理多个行为树

## 🚀 快速开始

### 1. 创建简单的AI行为

```csharp
// 自定义动作节点 - 寻找目标
public partial class FindTargetAction : ActionNode
{
    [Export] public float DetectionRange = 100f;
    
    protected override NodeStatus PerformAction(double delta)
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        var myPos = GetParent<CharacterBody2D>().GlobalPosition;
        
        foreach (Node2D enemy in enemies)
        {
            if (myPos.DistanceTo(enemy.GlobalPosition) <= DetectionRange)
            {
                Blackboard.Set("target", enemy);
                DebugLog($"找到目标: {enemy.Name}");
                return NodeStatus.Success;
            }
        }
        
        DebugLog("未找到目标");
        return NodeStatus.Failure;
    }
}

// 自定义动作节点 - 移动到目标
public partial class MoveToTargetAction : ActionNode
{
    [Export] public float Speed = 200f;
    [Export] public float StopDistance = 50f;
    
    protected override NodeStatus PerformAction(double delta)
    {
        var target = Blackboard.Get<Node2D>("target");
        if (target == null) return NodeStatus.Failure;
        
        var character = GetParent<CharacterBody2D>();
        var distance = character.GlobalPosition.DistanceTo(target.GlobalPosition);
        
        if (distance <= StopDistance)
        {
            DebugLog("到达目标");
            return NodeStatus.Success;
        }
        
        var direction = (target.GlobalPosition - character.GlobalPosition).Normalized();
        character.Velocity = direction * Speed;
        character.MoveAndSlide();
        
        DebugLog($"移动中，距离: {distance:F1}");
        return NodeStatus.Running;
    }
}

// 自定义条件节点 - 检查生命值
public partial class HealthCondition : ConditionNode
{
    [Export] public float MinHealthPercent = 0.3f;
    
    protected override bool CheckCondition()
    {
        var currentHealth = Blackboard.Get<float>("health", 100f);
        var maxHealth = Blackboard.Get<float>("maxHealth", 100f);
        var healthPercent = currentHealth / maxHealth;
        
        return healthPercent >= MinHealthPercent;
    }
}
```

### 2. 构建行为树场景

在Godot编辑器中创建场景结构：

```
Enemy (CharacterBody2D)
└── BehaviorTree
    └── SelectorNode (根节点)
        ├── SequenceNode (攻击序列)
        │   ├── HealthCondition (生命值检查)
        │   ├── FindTargetAction (寻找目标)
        │   └── MoveToTargetAction (移动攻击)
        └── PatrolAction (默认巡逻)
```

### 3. 初始化和使用

```csharp
public partial class Enemy : CharacterBody2D
{
    private BehaviorTree _behaviorTree;
    private float _health = 100f;
    
    public override void _Ready()
    {
        _behaviorTree = GetNode<BehaviorTree>("BehaviorTree");
        
        // 设置初始黑板数据
        _behaviorTree.Blackboard.Set("health", _health);
        _behaviorTree.Blackboard.Set("maxHealth", 100f);
        _behaviorTree.Blackboard.Set("patrolRadius", 300f);
        
        // 连接信号
        _behaviorTree.TreeCompleted += OnBehaviorCompleted;
        
        // 注册到全局管理器
        BehaviorTreeManager.Instance.RegisterBehaviorTree($"Enemy_{GetInstanceId()}", _behaviorTree);
    }
    
    private void OnBehaviorCompleted(NodeStatus status)
    {
        GD.Print($"行为树执行完成: {status}");
    }
    
    public void TakeDamage(float damage)
    {
        _health -= damage;
        _behaviorTree.Blackboard.Set("health", _health);
        
        if (_health <= 0)
        {
            _behaviorTree.Pause(); // 死亡时暂停AI
        }
    }
}
```

## 📖 节点类型详解

### 组合节点 (Composite)

- **SequenceNode** - 顺序执行，一个失败则整体失败
- **SelectorNode** - 选择执行，一个成功则整体成功
- **ParallelNode** - 并行执行，可配置成功/失败条件

### 装饰器节点 (Decorator)

- **InverterNode** - 反转子节点结果
- **RepeaterNode** - 重复执行子节点
- **CooldownNode** - 冷却控制，限制执行频率

### 叶子节点 (Leaf)

- **ActionNode** - 动作节点基类
- **ConditionNode** - 条件节点基类

## 🔧 高级功能

### 黑板数据共享

```csharp
// 在任意节点中读写共享数据
Blackboard.Set("playerPosition", player.GlobalPosition);
Blackboard.Set("alertLevel", 5);
Blackboard.Set("lastSeen", Time.GetUnixTimeFromSystem());

var target = Blackboard.Get<Node2D>("target");
var isAlert = Blackboard.Get<bool>("isAlert", false);
```

### 全局行为树管理

```csharp
// 获取管理器实例
var manager = BehaviorTreeManager.Instance;

// 暂停所有敌人AI
manager.PauseAllTrees();

// 重置特定行为树
var enemyTree = manager.GetBehaviorTree("Enemy_12345");
enemyTree?.ResetTree();

// 获取所有已注册的行为树
var treeNames = manager.GetRegisteredBehaviorTrees();
```

### 调试和监控

```csharp
// 启用调试模式
_behaviorTree.DebugMode = true;

// 调整执行频率
_behaviorTree.TickRate = 0.05f; // 每50毫秒执行一次

// 手动执行一次
var status = _behaviorTree.Tick(GetProcessDeltaTime());
```

## 📋 完整示例 - 守卫AI

```csharp
// 巡逻动作
public partial class PatrolAction : ActionNode
{
    [Export] public float PatrolRadius = 200f;
    [Export] public float Speed = 100f;
    
    private Vector2 _startPos;
    private Vector2 _targetPos;
    
    public override void Initialize()
    {
        var character = GetParent<CharacterBody2D>();
        _startPos = character.GlobalPosition;
        GenerateNewTarget();
    }
    
    protected override NodeStatus PerformAction(double delta)
    {
        var character = GetParent<CharacterBody2D>();
        var distance = character.GlobalPosition.DistanceTo(_targetPos);
        
        if (distance < 10f)
        {
            GenerateNewTarget();
            return NodeStatus.Success;
        }
        
        var direction = (_targetPos - character.GlobalPosition).Normalized();
        character.Velocity = direction * Speed;
        character.MoveAndSlide();
        
        return NodeStatus.Running;
    }
    
    private void GenerateNewTarget()
    {
        var angle = GD.Randf() * Mathf.Tau;
        var radius = GD.RandRange(50f, PatrolRadius);
        _targetPos = _startPos + Vector2.FromAngle(angle) * radius;
    }
}

// 攻击动作
public partial class AttackAction : ActionNode
{
    [Export] public float AttackRange = 80f;
    [Export] public float Damage = 25f;
    
    protected override NodeStatus PerformAction(double delta)
    {
        var target = Blackboard.Get<Node2D>("target");
        if (target == null) return NodeStatus.Failure;
        
        var character = GetParent<CharacterBody2D>();
        var distance = character.GlobalPosition.DistanceTo(target.GlobalPosition);
        
        if (distance > AttackRange)
        {
            DebugLog("目标超出攻击范围");
            return NodeStatus.Failure;
        }
        
        // 执行攻击
        if (target.HasMethod("TakeDamage"))
        {
            target.Call("TakeDamage", Damage);
            DebugLog($"攻击目标造成 {Damage} 伤害");
        }
        
        return NodeStatus.Success;
    }
}

// 目标丢失条件
public partial class HasTargetCondition : ConditionNode
{
    [Export] public float MaxDistance = 500f;
    
    protected override bool CheckCondition()
    {
        var target = Blackboard.Get<Node2D>("target");
        if (target == null || !IsInstanceValid(target))
        {
            Blackboard.Remove("target");
            return false;
        }
        
        var character = GetParent<CharacterBody2D>();
        var distance = character.GlobalPosition.DistanceTo(target.GlobalPosition);
        
        if (distance > MaxDistance)
        {
            Blackboard.Remove("target");
            DebugLog("目标距离过远，丢失目标");
            return false;
        }
        
        return true;
    }
}
```

## 🎯 最佳实践

1. **节点职责单一** - 每个节点只负责一个具体功能
2. **合理使用黑板** - 避免过度依赖全局状态
3. **调试友好** - 添加有意义的调试信息
4. **性能优化** - 根据需要调整执行频率
5. **状态管理** - 及时清理无效的黑板数据

## 🔧 扩展指南

继承对应的基类来创建自定义节点：

```csharp
// 自定义组合节点
public partial class RandomSelectorNode : CompositeNode
{
    public override NodeStatus Execute(double delta)
    {
        var shuffled = children.OrderBy(x => GD.Randf()).ToArray();
        // 实现随机选择逻辑...
    }
}

// 自定义装饰器节点  
public partial class TimeoutNode : DecoratorNode
{
    [Export] public float TimeoutSeconds = 5f;
    // 实现超时控制逻辑...
}
```

---

**BehaviorTree** - 让你的Godot C# 项目拥有智能而灵活的AI系统！