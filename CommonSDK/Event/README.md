# EventBus - 事件系统

一个为 Godot C# 项目设计的高性能、类型安全的事件总线系统，支持自动注册、优先级处理和异步事件处理。

## ✨ 特性

- 🔒 **类型安全** - 基于泛型的强类型事件系统
- ⚡ **高性能** - 优化的事件分发机制，支持优先级排序
- 🤖 **自动注册** - 自动发现和注册 Godot 节点中的事件处理程序
- 🎯 **优先级支持** - 支持事件处理程序优先级，控制执行顺序
- 🔄 **同步/异步** - 同时支持同步和异步事件处理
- 🧵 **线程安全** - 完全的线程安全设计
- 🔍 **调试友好** - 丰富的调试信息和统计功能
- 📱 **零依赖** - 仅依赖 Godot 引擎，无额外第三方库

## 🚀 快速开始

### 1. 定义事件

```csharp
// 所有事件都必须继承 EventBase
public class PlayerHealthChangedEvent : EventBase
{
    public int OldHealth { get; set; }
    public int NewHealth { get; set; }
    public float HealthPercentage => NewHealth / 100f;
}

public class PlayerLevelUpEvent : EventBase
{
    public int OldLevel { get; set; }
    public int NewLevel { get; set; }
    public int ExperienceGained { get; set; }
}
```

### 2. 创建事件订阅者

```csharp
// 标记类为 EventBus 订阅者
[EventBusSubscriber]
public partial class GameManager : Node
{
    // 高优先级异步处理程序
    [EventSubscribe(Priority = 100)]
    public async Task OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
    {
        GD.Print($"玩家血量变化: {evt.OldHealth} -> {evt.NewHealth}");
        
        if (evt.NewHealth <= 0)
        {
            await HandlePlayerDeath();
        }
    }
    
    // 默认优先级同步处理程序
    [EventSubscribe]
    public void OnPlayerLevelUp(PlayerLevelUpEvent evt)
    {
        GD.Print($"玩家升级: Lv.{evt.OldLevel} -> Lv.{evt.NewLevel}");
        ShowLevelUpEffect();
    }
    
    private async Task HandlePlayerDeath()
    {
        // 处理玩家死亡逻辑
        await Task.Delay(1000); // 模拟动画延迟
        GetTree().ReloadCurrentScene();
    }
    
    private void ShowLevelUpEffect()
    {
        // 显示升级特效
    }
}
```

### 3. 触发事件

```csharp
public partial class Player : CharacterBody2D
{
    private int _health = 100;
    private int _level = 1;
    
    public void TakeDamage(int damage)
    {
        int oldHealth = _health;
        _health = Mathf.Max(0, _health - damage);
        
        // 触发血量变化事件
        EventBus.TriggerEvent(new PlayerHealthChangedEvent 
        { 
            OldHealth = oldHealth, 
            NewHealth = _health 
        });
    }
    
    public async void LevelUp()
    {
        int oldLevel = _level;
        _level++;
        
        // 异步触发升级事件
        await EventBus.TriggerEventAsync(new PlayerLevelUpEvent 
        { 
            OldLevel = oldLevel, 
            NewLevel = _level,
            ExperienceGained = 1000
        });
    }
}
```

## 📖 详细使用指南

### 自动注册 vs 手动注册

#### 自动注册（推荐）

对于 Godot 节点，系统会自动发现并注册标记了 `[EventBusSubscriber]` 的类：

```csharp
[EventBusSubscriber]
public partial class UIManager : Control
{
    // 系统会自动注册这个方法
    [EventSubscribe(Priority = 50)]
    public void UpdateHealthBar(PlayerHealthChangedEvent evt)
    {
        var healthBar = GetNode<ProgressBar>("HealthBar");
        healthBar.Value = evt.HealthPercentage * 100;
    }
}
```

#### 手动注册

对于非节点类或需要精确控制的场景：

```csharp
public class GameStats
{
    public GameStats()
    {
        // 手动注册事件处理程序
        EventBus.RegisterEvent<PlayerHealthChangedEvent>(OnHealthChanged, priority: 10);
        EventBus.RegisterEvent<PlayerLevelUpEvent>(OnLevelUp);
    }
    
    private void OnHealthChanged(PlayerHealthChangedEvent evt)
    {
        // 更新统计数据
    }
    
    private async Task OnLevelUp(PlayerLevelUpEvent evt)
    {
        // 保存游戏数据
        await SaveGameData();
    }
}
```

### 优先级系统

事件处理程序按优先级执行，数值越大优先级越高：

```csharp
[EventBusSubscriber]
public partial class GameLogic : Node
{
    // 最高优先级 - 首先执行
    [EventSubscribe(Priority = 100)]
    public void ValidateAction(PlayerActionEvent evt)
    {
        // 验证玩家行为
    }
    
    // 中等优先级
    [EventSubscribe(Priority = 50)]
    public void ProcessAction(PlayerActionEvent evt)
    {
        // 处理行为逻辑
    }
    
    // 默认优先级 (0) - 最后执行
    [EventSubscribe]
    public void LogAction(PlayerActionEvent evt)
    {
        // 记录日志
    }
}
```

### 静态事件处理程序

支持静态方法作为事件处理程序：

```csharp
[EventBusSubscriber]
public static class GlobalEventHandlers
{
    [EventSubscribe(Priority = 1000)]
    public static void OnAnyEvent(EventBase evt)
    {
        // 全局事件日志
        GD.Print($"Event triggered: {evt.GetType().Name}");
    }
    
    [EventSubscribe]
    public static async Task OnError(ErrorEvent evt)
    {
        // 全局错误处理
        await ReportError(evt);
    }
}
```

### 清理和资源管理

系统会自动处理节点的清理，但也可以手动管理：

```csharp
public partial class TemporaryObject : Node
{
    public override void _ExitTree()
    {
        // 手动清理（通常不需要，系统会自动处理）
        this.UnregisterFromEventBus();
        base._ExitTree();
    }
}
```

## 🔧 高级功能

### 调试和监控

```csharp
// 获取系统统计信息
var stats = EventAutoRegHelper.GetStatistics();
GD.Print($"已注册节点: {stats.RegisteredNodes}");
GD.Print($"事件类型: {stats.EventTypes}");

// 获取特定事件的详细信息
var debugInfo = EventBus.GetEventTypeDebugInfo<PlayerHealthChangedEvent>();
GD.Print(debugInfo);

// 检查节点是否已注册
if (this.IsEventBusRegistered())
{
    GD.Print("节点已注册到 EventBus");
}
```

### 强制场景扫描

在特殊情况下，可以手动触发场景扫描：

```csharp
// 强制扫描当前场景的所有节点
EventAutoRegHelper.ForceScanCurrentScene();
```

### 批量操作

```csharp
// 注销对象的所有事件处理程序
EventBus.UnregisterAllEventsForObject(targetObject);

// 取消特定事件类型的所有处理程序
EventBus.CancelEvent<PlayerHealthChangedEvent>();

// 清理所有注册信息（慎用）
EventBus.UnregisterAllEvents();
```

## 📋 完整示例

以下是一个完整的游戏示例，展示了 EventBus 系统的各种用法：

```csharp
// === 事件定义 ===
public class GameStartEvent : EventBase 
{ 
    public string PlayerName { get; set; }
}

public class EnemySpawnedEvent : EventBase 
{ 
    public Vector2 Position { get; set; }
    public string EnemyType { get; set; }
}

public class ScoreChangedEvent : EventBase 
{ 
    public int OldScore { get; set; }
    public int NewScore { get; set; }
    public int PointsAdded { get; set; }
}

// === 游戏管理器 ===
[EventBusSubscriber]
public partial class GameController : Node
{
    private int _score = 0;
    private Timer _spawnTimer;
    
    public override void _Ready()
    {
        _spawnTimer = GetNode<Timer>("SpawnTimer");
        _spawnTimer.Timeout += OnSpawnTimer;
        
        // 开始游戏
        EventBus.TriggerEvent(new GameStartEvent { PlayerName = "Player1" });
    }
    
    [EventSubscribe(Priority = 100)]
    public void OnGameStart(GameStartEvent evt)
    {
        GD.Print($"游戏开始! 玩家: {evt.PlayerName}");
        _spawnTimer.Start();
    }
    
    private void OnSpawnTimer()
    {
        var spawnPos = new Vector2(
            GD.RandRange(0, GetViewportRect().Size.X),
            0
        );
        
        EventBus.TriggerEvent(new EnemySpawnedEvent 
        { 
            Position = spawnPos,
            EnemyType = "BasicEnemy"
        });
    }
    
    public void AddScore(int points)
    {
        int oldScore = _score;
        _score += points;
        
        EventBus.TriggerEvent(new ScoreChangedEvent 
        { 
            OldScore = oldScore,
            NewScore = _score,
            PointsAdded = points
        });
    }
}

// === 敌人管理器 ===
[EventBusSubscriber]
public partial class EnemyManager : Node2D
{
    [Export] public PackedScene EnemyScene { get; set; }
    
    [EventSubscribe]
    public void OnEnemySpawned(EnemySpawnedEvent evt)
    {
        var enemy = EnemyScene.Instantiate<Enemy>();
        enemy.Position = evt.Position;
        AddChild(enemy);
        
        GD.Print($"生成敌人: {evt.EnemyType} at {evt.Position}");
    }
}

// === UI 管理器 ===
[EventBusSubscriber]
public partial class UIManager : Control
{
    private Label _scoreLabel;
    
    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("ScoreLabel");
    }
    
    [EventSubscribe(Priority = 50)]
    public async Task OnScoreChanged(ScoreChangedEvent evt)
    {
        _scoreLabel.Text = $"Score: {evt.NewScore}";
        
        // 显示得分动画
        var tween = CreateTween();
        tween.TweenProperty(_scoreLabel, "modulate", Colors.Yellow, 0.2);
        tween.TweenProperty(_scoreLabel, "modulate", Colors.White, 0.2);
        
        await ToSignal(tween, Tween.SignalName.Finished);
    }
    
    [EventSubscribe]
    public void OnGameStart(GameStartEvent evt)
    {
        _scoreLabel.Text = "Score: 0";
        Show(); // 显示 UI
    }
}

// === 音频管理器 ===
[EventBusSubscriber]
public partial class AudioManager : AudioStreamPlayer
{
    [Export] public AudioStream ScoreSound { get; set; }
    [Export] public AudioStream EnemySpawnSound { get; set; }
    
    [EventSubscribe]
    public void OnScoreChanged(ScoreChangedEvent evt)
    {
        Stream = ScoreSound;
        Play();
    }
    
    [EventSubscribe]
    public void OnEnemySpawned(EnemySpawnedEvent evt)
    {
        Stream = EnemySpawnSound;
        Play();
    }
}

// === 敌人类 ===
public partial class Enemy : CharacterBody2D
{
    [Export] public float Speed = 200f;
    
    public override void _Ready()
    {
        // 敌人可以直接访问游戏控制器来加分
        var area = GetNode<Area2D>("Area2D");
        area.BodyEntered += OnBodyEntered;
    }
    
    public override void _PhysicsProcess(double delta)
    {
        Velocity = Vector2.Down * Speed;
        MoveAndSlide();
        
        // 移出屏幕时删除
        if (Position.Y > GetViewportRect().Size.Y + 100)
        {
            QueueFree();
        }
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (body.Name == "Player")
        {
            // 通过获取游戏控制器来加分
            var gameController = GetTree().GetFirstNodeInGroup("GameController") as GameController;
            gameController?.AddScore(100);
            
            QueueFree();
        }
    }
}
```

## 🔨 最佳实践

### 1. 事件设计

- **单一职责**: 每个事件应该表示一个明确的业务含义
- **不可变性**: 事件数据应该是只读的，避免处理程序之间的副作用
- **合理粒度**: 既不要过于细粒度也不要过于粗粒度

```csharp
// ✅ 好的设计
public class PlayerHealthChangedEvent : EventBase
{
    public int PlayerId { get; }
    public int OldHealth { get; }
    public int NewHealth { get; }
    public float HealthPercentage => NewHealth / 100f;
    
    public PlayerHealthChangedEvent(int playerId, int oldHealth, int newHealth)
    {
        PlayerId = playerId;
        OldHealth = oldHealth;
        NewHealth = newHealth;
    }
}

// ❌ 避免的设计
public class GameEvent : EventBase
{
    public object Data { get; set; } // 过于通用
    public string Type { get; set; } // 失去类型安全
}
```

### 2. 优先级使用

建议的优先级范围：
- **1000+**: 系统级处理（验证、安全检查）
- **100-999**: 核心业务逻辑
- **1-99**: UI 更新、音效播放
- **0**: 默认优先级
- **负数**: 日志记录、统计收集

### 3. 异步处理

对于可能耗时的操作，使用异步处理程序：

```csharp
[EventSubscribe]
public async Task OnPlayerSaved(PlayerSaveEvent evt)
{
    // 耗时的保存操作
    await SaveToDatabase(evt.PlayerData);
    await UploadToCloud(evt.PlayerData);
}
```

### 4. 错误处理

事件处理程序中的异常不会中断其他处理程序的执行：

```csharp
[EventSubscribe]
public void OnSomeEvent(SomeEvent evt)
{
    try
    {
        // 可能失败的操作
        RiskyOperation(evt);
    }
    catch (Exception ex)
    {
        GD.PrintErr($"处理事件时发生错误: {ex.Message}");
        // 可以选择触发错误事件
        EventBus.TriggerEvent(new ErrorEvent { Exception = ex });
    }
}
```

## ⚠️ 注意事项

1. **循环事件**: 避免在事件处理程序中触发可能导致循环的事件
2. **性能考虑**: 事件处理程序应该尽可能轻量，避免重计算
3. **内存泄漏**: 系统会自动清理节点注册，但手动注册的处理程序需要手动清理
4. **线程安全**: 虽然 EventBus 是线程安全的，但事件处理程序的具体实现需要自行保证线程安全

## 🐛 故障排除

### 事件没有被触发

1. 检查类是否标记了 `[EventBusSubscriber]`
2. 检查方法是否标记了 `[EventSubscribe]`
3. 检查方法签名是否正确（一个 EventBase 参数，返回 void 或 Task）
4. 确认节点已被添加到场景树中

### 自动注册不工作

```csharp
// 手动检查注册状态
if (!this.IsEventBusRegistered())
{
    GD.PrintErr("节点未自动注册，尝试手动注册");
    this.RegisterToEventBus();
}

// 查看详细统计
var stats = EventAutoRegHelper.GetDetailedStatistics();
GD.Print(stats);
```

### 性能问题

```csharp
// 检查注册的处理程序数量
var eventTypeCount = EventBus.GetRegisteredEventTypeCount();
var instanceCount = EventBus.GetRegisteredInstanceCount();

if (eventTypeCount > 100 || instanceCount > 1000)
{
    GD.PrintErr("EventBus 注册过多，可能存在内存泄漏");
}
```