# StateMachine - 状态机系统

一个为 Godot C# 项目设计的完整状态机系统，支持状态管理、数据传递、调试监控和全局管理功能。

## ✨ 特性

- 🎯 **状态管理** - 完整的状态生命周期管理和切换控制
- 📡 **数据传递** - 状态间数据共享和参数传递
- 🔧 **组件化** - 可挂载到任意节点的组件式状态机
- 🌍 **全局管理** - 单例管理器统一管理多个状态机
- 🐛 **调试支持** - 内置调试日志和状态变化监控
- 🎮 **输入处理** - 完整的输入事件分发机制
- ⚡ **高性能** - 优化的更新循环和事件处理

## 🚀 快速开始

### 1. 创建基本状态

```csharp
// 空闲状态
public partial class IdleState : State
{
    public override void Enter()
    {
        DebugLog("进入空闲状态");
        var player = GetParent<Player>();
        player.PlayAnimation("idle");
    }
    
    public override void HandleInput(InputEvent @event)
    {
        if (@event.IsActionPressed("move"))
        {
            StateMachine.ChangeState("Moving");
        }
        else if (@event.IsActionPressed("attack"))
        {
            StateMachine.ChangeState("Attacking");
        }
    }
    
    public override void Exit()
    {
        DebugLog("离开空闲状态");
    }
}

// 移动状态
public partial class MovingState : State
{
    public override void Enter()
    {
        DebugLog("进入移动状态");
        var player = GetParent<Player>();
        player.PlayAnimation("walk");
    }
    
    public override void PhysicsUpdate(double delta)
    {
        var player = GetParent<Player>();
        
        var inputVector = Vector2.Zero;
        inputVector.X = Input.GetAxis("move_left", "move_right");
        inputVector.Y = Input.GetAxis("move_up", "move_down");
        
        if (inputVector.Length() > 0)
        {
            player.Velocity = inputVector.Normalized() * 200;
            player.MoveAndSlide();
        }
        else
        {
            StateMachine.ChangeState("Idle");
        }
    }
}

// 攻击状态
public partial class AttackingState : State
{
    private float _attackDuration = 0.5f;
    private float _attackTimer;
    
    public override void Enter()
    {
        DebugLog("进入攻击状态");
        _attackTimer = _attackDuration;
        
        var player = GetParent<Player>();
        player.PlayAnimation("attack");
        
        // 设置状态数据
        SetStateData("isAttacking", true);
        SetStateData("attackStartTime", Time.GetUnixTimeFromSystem());
    }
    
    public override void Update(double delta)
    {
        _attackTimer -= (float)delta;
        if (_attackTimer <= 0)
        {
            StateMachine.ChangeState("Idle");
        }
    }
    
    public override void Exit()
    {
        DebugLog("离开攻击状态");
        SetStateData("isAttacking", false);
    }
}
```

### 2. 设置状态机

```csharp
public partial class Player : CharacterBody2D
{
    private StateMachine _stateMachine;
    
    public override void _Ready()
    {
        // 创建状态机
        _stateMachine = new StateMachine();
        _stateMachine.DebugMode = true;
        AddChild(_stateMachine);
        
        // 创建状态
        var idleState = new IdleState { Name = "Idle" };
        var movingState = new MovingState { Name = "Moving" };
        var attackingState = new AttackingState { Name = "Attacking" };
        
        _stateMachine.AddChild(idleState);
        _stateMachine.AddChild(movingState);
        _stateMachine.AddChild(attackingState);
        
        // 设置初始状态
        _stateMachine.InitialState = idleState;
        
        // 连接状态变化信号
        _stateMachine.StateChanged += OnStateChanged;
    }
    
    private void OnStateChanged(string oldStateName, string newStateName)
    {
        GD.Print($"玩家状态变化: {oldStateName} -> {newStateName}");
    }
    
    public void PlayAnimation(string animationName)
    {
        // 播放动画逻辑
        GD.Print($"播放动画: {animationName}");
    }
}
```

## 📖 详细功能指南

### 使用组件式状态机

```csharp
// 在场景中直接使用组件
public partial class Enemy : CharacterBody2D
{
    private StateMachineComponent _stateMachineComponent;
    
    public override void _Ready()
    {
        // 获取状态机组件
        _stateMachineComponent = GetNode<StateMachineComponent>("StateMachineComponent");
        
        // 连接状态变化事件
        _stateMachineComponent.StateMachine.StateChanged += OnStateChanged;
    }
    
    private void OnStateChanged(string oldState, string newState)
    {
        GD.Print($"敌人状态变化: {oldState} -> {newState}");
    }
    
    public void TakeDamage(int damage)
    {
        // 切换到受伤状态
        _stateMachineComponent.StateMachine.ChangeState("Hurt");
        
        // 传递伤害数据
        _stateMachineComponent.StateMachine.SetStateData("damage", damage);
    }
}
```

### 状态间数据传递

```csharp
// 商店状态
public partial class ShopState : State
{
    public override void Enter()
    {
        DebugLog("进入商店状态");
        
        // 获取玩家金币
        var playerGold = GetStateData<int>("playerGold", 0);
        DebugLog($"玩家金币: {playerGold}");
        
        // 显示商店UI
        ShowShopUI();
    }
    
    public void BuyItem(string itemName, int cost)
    {
        var playerGold = GetStateData<int>("playerGold", 0);
        
        if (playerGold >= cost)
        {
            // 扣除金币
            SetStateData("playerGold", playerGold - cost);
            
            // 添加物品到背包
            var inventory = GetStateData<List<string>>("inventory", new List<string>());
            inventory.Add(itemName);
            SetStateData("inventory", inventory);
            
            DebugLog($"购买物品: {itemName}, 花费: {cost}");
        }
        else
        {
            DebugLog("金币不足", LogType.Warn);
        }
    }
    
    private void ShowShopUI() { /* 显示商店UI逻辑 */ }
}

// 背包状态
public partial class InventoryState : State
{
    public override void Enter()
    {
        DebugLog("进入背包状态");
        
        // 获取背包物品
        var inventory = GetStateData<List<string>>("inventory", new List<string>());
        DisplayInventory(inventory);
    }
    
    private void DisplayInventory(List<string> items)
    {
        DebugLog($"背包物品数量: {items.Count}");
        foreach (var item in items)
        {
            DebugLog($"- {item}");
        }
    }
}
```

## 📋 完整示例 - RPG角色状态机

```csharp
// RPG角色控制器
public partial class RPGCharacter : CharacterBody2D
{
    [Export] public int Health = 100;
    [Export] public int MaxHealth = 100;
    [Export] public int Mana = 50;
    [Export] public int MaxMana = 50;
    [Export] public float Speed = 200f;
    
    private StateMachine _stateMachine;
    private AnimatedSprite2D _sprite;
    
    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("Sprite");
        SetupStateMachine();
    }
    
    private void SetupStateMachine()
    {
        _stateMachine = new StateMachine();
        _stateMachine.DebugMode = true;
        AddChild(_stateMachine);
        
        // 创建所有状态
        var states = new Dictionary<string, State>
        {
            ["Idle"] = new RPGIdleState(),
            ["Moving"] = new RPGMovingState(),
            ["Attacking"] = new RPGAttackingState(),
            ["Casting"] = new RPGCastingState(),
            ["Hurt"] = new RPGHurtState(),
            ["Dead"] = new RPGDeadState(),
            ["Stunned"] = new RPGStunnedState()
        };
        
        // 添加状态到状态机
        foreach (var kvp in states)
        {
            kvp.Value.Name = kvp.Key;
            _stateMachine.AddChild(kvp.Value);
        }
        
        // 设置初始状态
        _stateMachine.InitialState = states["Idle"];
        
        // 初始化状态数据
        InitializeStateData();
        
        // 连接事件
        _stateMachine.StateChanged += OnStateChanged;
    }
    
    private void InitializeStateData()
    {
        _stateMachine.SetStateData("character", this);
        _stateMachine.SetStateData("health", Health);
        _stateMachine.SetStateData("maxHealth", MaxHealth);
        _stateMachine.SetStateData("mana", Mana);
        _stateMachine.SetStateData("maxMana", MaxMana);
        _stateMachine.SetStateData("speed", Speed);
    }
    
    private void OnStateChanged(string oldState, string newState)
    {
        GD.Print($"角色状态: {oldState} -> {newState}");
    }
    
    public void TakeDamage(int damage)
    {
        Health = Mathf.Max(0, Health - damage);
        _stateMachine.SetStateData("health", Health);
        
        if (Health <= 0)
        {
            _stateMachine.ChangeState("Dead");
        }
        else if (!_stateMachine.IsCurrentState("Hurt"))
        {
            _stateMachine.SetStateData("damage", damage);
            _stateMachine.ChangeState("Hurt");
        }
    }
    
    public void Heal(int healAmount)
    {
        Health = Mathf.Min(MaxHealth, Health + healAmount);
        _stateMachine.SetStateData("health", Health);
    }
    
    public bool UseMana(int cost)
    {
        if (Mana >= cost)
        {
            Mana -= cost;
            _stateMachine.SetStateData("mana", Mana);
            return true;
        }
        return false;
    }
    
    public StateMachine GetStateMachine() => _stateMachine;
}

// RPG空闲状态
public partial class RPGIdleState : State
{
    private RPGCharacter character;
    
    public override void Enter()
    {
        character = GetStateData<RPGCharacter>("character");
        character.GetNode<AnimatedSprite2D>("Sprite").Play("idle");
        DebugLog("角色进入空闲状态");
    }
    
    public override void HandleInput(InputEvent @event)
    {
        // 移动输入
        var inputVector = Vector2.Zero;
        inputVector.X = Input.GetAxis("move_left", "move_right");
        inputVector.Y = Input.GetAxis("move_up", "move_down");
        
        if (inputVector.Length() > 0)
        {
            StateMachine.ChangeState("Moving");
            return;
        }
        
        // 攻击输入
        if (@event.IsActionPressed("attack"))
        {
            StateMachine.ChangeState("Attacking");
            return;
        }
        
        // 施法输入
        if (@event.IsActionPressed("cast_spell"))
        {
            var mana = GetStateData<int>("mana");
            if (mana >= 10)
            {
                StateMachine.ChangeState("Casting");
            }
            else
            {
                DebugLog("法力不足", LogType.Warn);
            }
        }
    }
}

// RPG移动状态
public partial class RPGMovingState : State
{
    private RPGCharacter character;
    private float speed;
    
    public override void Enter()
    {
        character = GetStateData<RPGCharacter>("character");
        speed = GetStateData<float>("speed");
        character.GetNode<AnimatedSprite2D>("Sprite").Play("walk");
        DebugLog("角色开始移动");
    }
    
    public override void PhysicsUpdate(double delta)
    {
        var inputVector = Vector2.Zero;
        inputVector.X = Input.GetAxis("move_left", "move_right");
        inputVector.Y = Input.GetAxis("move_up", "move_down");
        
        if (inputVector.Length() > 0)
        {
            character.Velocity = inputVector.Normalized() * speed;
            character.MoveAndSlide();
            
            // 翻转精灵
            if (inputVector.X != 0)
            {
                character.GetNode<AnimatedSprite2D>("Sprite").FlipH = inputVector.X < 0;
            }
        }
        else
        {
            StateMachine.ChangeState("Idle");
        }
    }
    
    public override void HandleInput(InputEvent @event)
    {
        if (@event.IsActionPressed("attack"))
        {
            StateMachine.ChangeState("Attacking");
        }
    }
}

// RPG攻击状态
public partial class RPGAttackingState : State
{
    private RPGCharacter character;
    private float attackTimer = 0f;
    private float attackDuration = 0.6f;
    private bool hasDealtDamage = false;
    
    public override void Enter()
    {
        character = GetStateData<RPGCharacter>("character");
        character.GetNode<AnimatedSprite2D>("Sprite").Play("attack");
        
        attackTimer = attackDuration;
        hasDealtDamage = false;
        
        DebugLog("角色开始攻击");
    }
    
    public override void Update(double delta)
    {
        attackTimer -= (float)delta;
        
        // 在攻击动画中间造成伤害
        if (!hasDealtDamage && attackTimer <= attackDuration * 0.5f)
        {
            DealDamage();
            hasDealtDamage = true;
        }
        
        if (attackTimer <= 0)
        {
            StateMachine.ChangeState("Idle");
        }
    }
    
    private void DealDamage()
    {
        // 检测攻击范围内的敌人
        var attackArea = character.GetNode<Area2D>("AttackArea");
        if (attackArea != null)
        {
            var enemies = attackArea.GetOverlappingBodies();
            foreach (Node2D enemy in enemies)
            {
                if (enemy.IsInGroup("enemies") && enemy.HasMethod("TakeDamage"))
                {
                    enemy.Call("TakeDamage", 25);
                    DebugLog($"对 {enemy.Name} 造成伤害");
                }
            }
        }
    }
}

// RPG施法状态
public partial class RPGCastingState : State
{
    private RPGCharacter character;
    private float castTimer = 0f;
    private float castDuration = 1.5f;
    
    public override void Enter()
    {
        character = GetStateData<RPGCharacter>("character");
        character.GetNode<AnimatedSprite2D>("Sprite").Play("cast");
        
        castTimer = castDuration;
        
        // 消耗法力
        if (!character.UseMana(10))
        {
            StateMachine.ChangeState("Idle");
            return;
        }
        
        DebugLog("角色开始施法");
    }
    
    public override void Update(double delta)
    {
        castTimer -= (float)delta;
        
        if (castTimer <= 0)
        {
            CastSpell();
            StateMachine.ChangeState("Idle");
        }
    }
    
    public override void HandleInput(InputEvent @event)
    {
        // 施法过程中可以被移动打断
        var inputVector = Vector2.Zero;
        inputVector.X = Input.GetAxis("move_left", "move_right");
        inputVector.Y = Input.GetAxis("move_up", "move_down");
        
        if (inputVector.Length() > 0)
        {
            DebugLog("施法被打断", LogType.Warn);
            StateMachine.ChangeState("Moving");
        }
    }
    
    private void CastSpell()
    {
        DebugLog("法术施放成功");
        // 创建法术效果
        CreateSpellEffect();
    }
    
    private void CreateSpellEffect()
    {
        // 创建法术特效和伤害
        DebugLog("创建法术特效");
    }
}

// RPG受伤状态
public partial class RPGHurtState : State
{
    private RPGCharacter character;
    private float hurtTimer = 0f;
    private float hurtDuration = 0.3f;
    
    public override void Enter()
    {
        character = GetStateData<RPGCharacter>("character");
        character.GetNode<AnimatedSprite2D>("Sprite").Play("hurt");
        
        hurtTimer = hurtDuration;
        
        var damage = GetStateData<int>("damage", 0);
        DebugLog($"角色受到 {damage} 点伤害");
        
        // 击退效果
        ApplyKnockback();
    }
    
    public override void Update(double delta)
    {
        hurtTimer -= (float)delta;
        
        if (hurtTimer <= 0)
        {
            StateMachine.ChangeState("Idle");
        }
    }
    
    private void ApplyKnockback()
    {
        // 简单的击退效果
        var knockbackDirection = Vector2.Right; // 可以根据伤害来源计算
        var knockbackForce = 100f;
        
        character.Velocity = knockbackDirection * knockbackForce;
        character.MoveAndSlide();
    }
    
    public override void Exit()
    {
        DebugLog("角色从受伤状态恢复");
    }
}

// RPG死亡状态
public partial class RPGDeadState : State
{
    private RPGCharacter character;
    private float respawnTimer = 0f;
    private float respawnDuration = 3f;
    
    public override void Enter()
    {
        character = GetStateData<RPGCharacter>("character");
        character.GetNode<AnimatedSprite2D>("Sprite").Play("death");
        
        respawnTimer = respawnDuration;
        DebugLog("角色死亡");
        
        // 禁用碰撞
        character.GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
    }
    
    public override void Update(double delta)
    {
        respawnTimer -= (float)delta;
        
        if (respawnTimer <= 0)
        {
            Respawn();
        }
    }
    
    private void Respawn()
    {
        DebugLog("角色复活");
        
        // 恢复生命值
        var maxHealth = GetStateData<int>("maxHealth");
        SetStateData("health", maxHealth);
        character.Health = maxHealth;
        
        // 启用碰撞
        character.GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", false);
        
        // 传送到复活点
        character.GlobalPosition = Vector2.Zero; // 或其他复活点位置
        
        StateMachine.ChangeState("Idle");
    }
    
    public override void HandleInput(InputEvent @event)
    {
        // 死亡状态下不响应输入
    }
}

// RPG眩晕状态
public partial class RPGStunnedState : State
{
    private RPGCharacter character;
    private float stunTimer = 0f;
    
    public override void Enter()
    {
        character = GetStateData<RPGCharacter>("character");
        character.GetNode<AnimatedSprite2D>("Sprite").Play("stunned");
        
        var stunDuration = GetStateData<float>("stunDuration", 2f);
        stunTimer = stunDuration;
        
        DebugLog($"角色被眩晕 {stunDuration} 秒");
        
        // 添加眩晕特效
        ShowStunEffect();
    }
    
    public override void Update(double delta)
    {
        stunTimer -= (float)delta;
        
        if (stunTimer <= 0)
        {
            StateMachine.ChangeState("Idle");
        }
    }
    
    public override void HandleInput(InputEvent @event)
    {
        // 眩晕状态下不响应输入
    }
    
    private void ShowStunEffect()
    {
        // 显示眩晕特效
        DebugLog("显示眩晕特效");
    }
    
    public override void Exit()
    {
        DebugLog("角色从眩晕中恢复");
        HideStunEffect();
    }
    
    private void HideStunEffect()
    {
        // 隐藏眩晕特效
        DebugLog("隐藏眩晕特效");
    }
}
```

## 🔧 高级功能

### 全局状态机管理

```csharp
public partial class GameManager : Node
{
    public override void _Ready()
    {
        var manager = StateMachineManager.Instance;
        
        // 注册重要的状态机
        var playerStateMachine = GetNode<Player>("Player").GetStateMachine();
        manager.RegisterStateMachine("Player", playerStateMachine);
        
        var gameStateMachine = GetNode<GameController>("GameController").GetStateMachine();
        manager.RegisterStateMachine("Game", gameStateMachine);
    }
    
    public void PauseGame()
    {
        var manager = StateMachineManager.Instance;
        
        // 暂停玩家状态机
        var playerSM = manager.GetStateMachine("Player");
        playerSM?.ChangeState("Paused");
        
        // 切换游戏状态
        var gameSM = manager.GetStateMachine("Game");
        gameSM?.ChangeState("Paused");
    }
}
```

### 状态条件检查

```csharp
public partial class ConditionalState : State
{
    public override void Enter()
    {
        // 检查进入条件
        if (!CanEnterState())
        {
            DebugLog("条件不满足，无法进入状态", LogType.Warn);
            StateMachine.ChangeToPreviousState();
            return;
        }
        
        DebugLog("条件满足，进入状态");
    }
    
    private bool CanEnterState()
    {
        var health = GetStateData<int>("health");
        var mana = GetStateData<int>("mana");
        
        return health > 0 && mana >= 20;
    }
}
```

## 🎯 最佳实践

### 1. 状态设计原则

```csharp
// ✅ 好的状态设计 - 职责单一
public partial class JumpingState : State
{
    public override void Enter()
    {
        // 只处理跳跃相关的逻辑
        StartJump();
    }
    
    public override void PhysicsUpdate(double delta)
    {
        // 只处理跳跃物理
        UpdateJumpPhysics(delta);
        
        // 检查跳跃结束条件
        if (IsGrounded())
        {
            StateMachine.ChangeState("Idle");
        }
    }
}

// ❌ 避免 - 职责过多的状态
public partial class BadState : State
{
    public override void Update(double delta)
    {
        // 不好：一个状态处理太多不相关的逻辑
        HandleMovement();
        HandleAttack();
        HandleInventory();
        HandleDialogue();
    }
}
```

### 2. 合理的状态切换

```csharp
// ✅ 清晰的状态切换逻辑
public partial class CombatState : State
{
    public override void HandleInput(InputEvent @event)
    {
        if (@event.IsActionPressed("dodge"))
        {
            if (CanDodge())
            {
                StateMachine.ChangeState("Dodging");
            }
        }
        else if (@event.IsActionPressed("block"))
        {
            if (CanBlock())
            {
                StateMachine.ChangeState("Blocking");
            }
        }
    }
    
    private bool CanDodge()
    {
        var stamina = GetStateData<float>("stamina");
        return stamina >= 20f;
    }
    
    private bool CanBlock()
    {
        var hasShield = GetStateData<bool>("hasShield");
        return hasShield;
    }
}
```

### 3. 数据管理

```csharp
// ✅ 结构化的状态数据管理
public partial class PlayerStateMachine : StateMachine
{
    public void InitializePlayerData(Player player)
    {
        // 集中初始化所有状态数据
        SetStateData("player", player);
        SetStateData("health", player.Health);
        SetStateData("maxHealth", player.MaxHealth);
        SetStateData("speed", player.Speed);
        SetStateData("inventory", player.Inventory);
        SetStateData("abilities", player.Abilities);
    }
    
    public void UpdatePlayerStats(Player player)
    {
        // 同步玩家数据
        SetStateData("health", player.Health);
        SetStateData("position", player.GlobalPosition);
    }
}
```

---

**StateMachine** - 让你的 Godot C# 项目拥有强大而灵活的状态管理能力！