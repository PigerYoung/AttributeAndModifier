# AK-INVALIDATE：属性失效沿下游传播

本文是实现说明。搜索标签是 `AK-INVALIDATE`。代码注释、课程断言失败信息都带这个标签，读代码时用它定位这一次修改。

不要做别的重构。不要引入全局 epoch，不要在 `AttributeChanged` 里标脏，不要为等级、时间、日期各写一个 `xxxSensitive` 字段。

## 1. 这次修改解决什么

属性值有缓存。缓存只在 `slot.Dirty == true` 时重算，否则 `AttributeInstance.Value` 直接返回 `slot.Cached`。

现在脏标记停在被修改的那个属性上，下游和一部分修饰器来源看不到这次修改，于是一直用旧缓存。读路径本身不用改：仍然是脏了才 `Recompute`，不脏就返回缓存。修改只发生在写入侧。

三类失效共用一个入口 `AttributeContext.Invalidate`。它只把相关属性标脏，不立刻重算。

| 失效 | 边从哪来 | 例子 |
| --- | --- | --- |
| 静态闭包 | 冻结时的公式和 `DependsOn` | 力量 10→80，不读力量，伤害从 210 变为 280 |
| 运行期反向边 | `AddModifier` 时的属性引用 | 攻击力 `+ Strength × 0.5`，力量 10→20，攻击力从 5 变为 10 |
| 上下文输入订阅 | 等级、已过时间、`ContextFloat`，以及以后的日期 | 等级 1→3，曲线值从 10 变为 20；时间 0→2，衰减值从 80 变为 40 |

属性图是「谁依赖谁」。失效沿着反方向走：源变了，依赖它的人变脏。

## 2. 当前代码为什么会留下旧缓存

实现时不要保留下面这些旧行为。

`AttributeRegistry.BuildDependencyGraphOrThrow` 只存直接下游。`MaxHealth → HealthFromMaxHealth` 有边，`Strength → AttackFromStrength → Damage` 这种第二跳没有被一次标完。

`AttributeContext.OnAttributeValueChanged` 在最终值已经重算并且变化超过 `0.00001` 之后，才标直接下游。`SetBase`、添加或移除修饰器不会走到这里。力量改完后如果没人读力量，攻击力保持干净。

`AttributeInstance.BaseValue` 只把自己的 `slot.Dirty` 设为 true，不调用 `_calculator.Invalidate()`，也不通知下游。

`MagnitudeSource.Attribute` 和 Pipeline 里的 `AttributeRefModifier`（`ModifierTypeId == 3`）不在冻结图里。Buff 是运行中加上的，注册属性时写不进 `DependsOn`。

`Level`、`ElapsedTime` 是自动属性。`LevelCurve`（枚举值 `MagnitudeSourceType.Scalable`）读 `Level`，`TimeDecay` 读 `ElapsedTime`。这两个 setter 现在不标脏。`ModifierCalculator` 对非 `Fixed` 来源会跳过自己的缓存，但是外层 `slot.Dirty` 仍是 false，计算进不去。

`SetFloat` / `SetInt` 只把持有对应 `ContextFloat` 修饰器的实例标脏，不向该实例的下游传播。

`CurrentTime` 不参与当前数值公式。`TimeDecay` 用的是 `ElapsedTime`。不要在 `CurrentTime` setter 里失效。

`MagnitudeSourceType.SkillLevelParameter` 和 `HeroLevelSkillParameter` 目前 `Calculate` 落到 `Data0`，不读上下文。这次不要为它们登记订阅。以后它们若开始读某个输入，按第 7 节加键。

## 3. 行为契约

写入后：

- 被改到的属性是脏的。
- 它的全部静态下游是脏的。
- 通过运行期修饰器引用它的属性是脏的，那些属性自己的静态下游也是脏的。
- 等级、已过时间、上下文浮点只弄脏订阅了该输入的属性，再沿上面的规则往下传。
- 尚未 `GetOrCreate` 的属性不用标。它们第一次创建时 `Dirty` 已经是 true。
- 不创建空的 `AttributeGroup`。分组还不存在就跳过。
- 不因为标脏触发 `AttributeChanged`。`Changed` 仍只在 `Recompute` 算出的新值和旧缓存相差超过 `0.00001` 时发生。
- 同一次失效里，同一个属性只处理一次。
- 数值不变则什么都不做。比较阈值沿用 `0.00001f`。`SetBase`、`Level`、`ElapsedTime`、`SetFloat`、`SetInt` 都是这样。

读取后：

- `GetValue` 看到脏就 `Recompute`。公式里的 `ctx.GetValue(依赖)` 会把脏的上游一并算掉。
- 所以 A→B→C 时，只读 C 就会先算 A，再算 B，再算 C。
- 第二次读 C，直接返回缓存。

## 4. 三个例子

这三个例子是 `AK-INVALIDATE` 存在的原因。注释和课程都用同一组数字。

### 4.1 静态闭包

目录末尾新增两个属性，不要插到 `Lesson.Strength` 前面，避免已有 `AttributeId` 错位。

```text
Lesson.AttackFromStrength
    基础值 100
    公式 base + add + Lesson.Strength * 0.5
    DependsOn Lesson.Strength

Lesson.DamageFromAttack
    基础值 0
    公式 Lesson.AttackFromStrength * 2
    DependsOn Lesson.AttackFromStrength
```

冻结后的闭包：

```text
Strength 的全部下游 = [AttackFromStrength, DamageFromAttack]
AttackFromStrength 的全部下游 = [DamageFromAttack]
DamageFromAttack 的全部下游 = []
```

`MaxHealth` 的下游里同时有 `Health` 和 `HealthFromMaxHealth`。`Health` 只是声明了 `DependsOn`，默认公式不读 `MaxHealth`，不断言它的数值会变。断言 `HealthFromMaxHealth`。

过程：

```text
SetBase(Strength, 10)          // 默认已经是 10 时这句不会标脏
SetBase(AttackFromStrength, 100)
GetValue(DamageFromAttack)     // 210 = (100 + 10 * 0.5) * 2，然后三者都干净

SetBase(Strength, 80)          // Invalidate(Strength)
                               // Strength、AttackFromStrength、DamageFromAttack 都是脏的
                               // 此时还没有重算，缓存仍是 10 / 105 / 210

GetValue(DamageFromAttack)     // 只读伤害
                               // Damage 脏 → 读 AttackFromStrength
                               // AttackFromStrength 脏 → 读 Strength
                               // Strength 重算为 80
                               // AttackFromStrength 重算为 140
                               // DamageFromAttack 重算为 280
```

课程里必须删掉现在的 `context.GetValue(LessonCatalog.MaxHealth)`。这行是旧实现的绕行：先读源属性，`Changed` 才有机会标下游。修改后不读 `MaxHealth`，`HealthFromMaxHealth` 也要从 800 变成 500。

```text
SetBase(MaxHealth, 800)
SetBase(HealthFromMaxHealth, 1000)
GetValue(HealthFromMaxHealth)  // 800 = min(1000, 800)

SetBase(MaxHealth, 500)        // 不读 MaxHealth
GetValue(HealthFromMaxHealth)  // 500
```

### 4.2 运行期反向边

攻击力基础值设为 0，加上：

```csharp
ModifierData.Add(Attack.ModifierKey, MagnitudeSource.Attribute(Strength.ModifierKey, 0.5f))
```

`AddModifier` 登记反向边 `Strength → Attack`，引用计数 1，然后 `Invalidate(Attack)`。攻击力的静态下游跟着脏。这里用第 4.1 节以外的下游，避免和公式链混在一起：

```text
Lesson.BonusFromAttack
    基础值 0
    公式 Lesson.Attack * 2
    DependsOn Lesson.Attack
```

过程：

```text
SetBase(Attack, 0)
SetBase(Strength, 10)
AddModifier(Attack, Attribute(Strength, 0.5))
GetValue(Attack)               // 5
GetValue(BonusFromAttack)      // 10

SetBase(Strength, 20)          // 不读 Strength，也不读 Attack
GetValue(BonusFromAttack)      // 20
GetValue(Attack)               // 10
```

两个 Buff 都引用力量时计数为 2。卸掉一个，边还在，力量变化仍然弄脏攻击力。两个都卸掉，计数到 0，边删除。之后 `SetBase(Strength)` 不再因为这条修饰器弄脏攻击力。攻击力自己被移除修饰器时仍然要 `Invalidate(Attack)`，因为它的修饰器列表变了。

Pipeline 里的属性引用同样登记。`SingleModifierData.TypeId == 3` 时，`Param0` 是 `ModifierKey.Packed`，也就是源属性的 `RawId`。不要调用 `SingleModifierData.Modify` 或 `CreateModifier`，那会分配对象。

### 4.3 上下文输入订阅

订阅表的键是字符串，不是每种子类型一个字段。内置键：

```text
"level"        LevelCurve / Scalable，以及 Pipeline 的 LevelCurveModifier（TypeId 2）
"elapsedTime"  TimeDecay，以及 Pipeline 的 TimeDecayModifier（TypeId 1）
ContextFloat 的 key
    修饰器自己的字符串，例如 "buffPower"
```

等级：

```text
Lesson.LevelProbe 基础值 0
AddModifier(LevelProbe, MagnitudeSource.LevelCurve(10f, new[] { 1f, 1f, 3f, 2f }, 1f))
Level = 1
GetValue(LevelProbe)           // 10 = 10 * 1 * 曲线(1)
Level = 3                      // InvalidateInput("level")，不碰 TimeProbe
GetValue(LevelProbe)           // 20 = 10 * 1 * 曲线(3)
```

`MagnitudeSource.LevelCurve` 的枚举值是 `Scalable`。`CalculateScalable` 返回 `Data0 * Data1 * InterpolateCurve(level, curve)`。曲线按 `[level, value, level, value]` 成对存放。

时间：

```text
Lesson.TimeProbe 基础值 0
AddModifier(TimeProbe, MagnitudeSource.TimeDecay(80f, 4f, DecayType.Linear))
ElapsedTime = 0
GetValue(TimeProbe)            // 80
ElapsedTime = 2                // InvalidateInput("elapsedTime")，不碰 LevelProbe
GetValue(TimeProbe)            // 40 = 80 * (1 - 0.5)
```

上下文浮点，并检查下游：

```text
AddModifier(Attack, MagnitudeSource.ContextFloat("buffPower", 1f, 0f))
SetBase(Attack, 0)
GetValue(BonusFromAttack)      // 0
SetFloat("buffPower", 5)       // 不读 Attack
GetValue(BonusFromAttack)      // 10 = (0 + 5) * 1 * 2
```

以后若增加「日期」这种和 `LevelCurve` 同类的来源，不要新增 `daySensitive`。来源在 `AddModifier` 时声明自己读 `"day"`，`Day` setter 调用 `InvalidateInput("day")`。力量变化仍走反向边，换日只走 `"day"` 这张订阅。攻击力的静态下游不用知道日期。

## 5. 总入口

在 `AttributeContext` 上实现。每个 Context 一份运行期索引，因为 Buff 和等级属于某一个实体。静态闭包在 `AttributeRegistry` 上，冻结后所有 Context 共享。

```csharp
void Invalidate(AttributeId id)
void InvalidateInput(string key)
```

`InvalidateInput` 找出该键的全部持有者，用同一次遍历代号调用内部 `Visit`。两个持有者的公共下游只走一次。

一次 `Visit(id)` 的顺序固定：

1. `id` 无效则返回。
2. 这一轮已经访问过 `id` 则返回。运行期边可能成环，这里截断。
3. 若该属性的分组和实例已经存在，把实例标脏，并 `_calculator.Invalidate()`。不存在就跳过，不要 `GetOrCreate`。
4. 取出静态闭包（不含自己，已去重）。对其中尚未访问的节点做第 3 步。
5. 走 `id` 的运行期反向边，对每个持有者 `Visit`。
6. 再走闭包里每个节点的运行期反向边。

第 6 步不能省。闭包数组里没有运行期边。若第 4 步已经把攻击力标成访问过，第 5 步对攻击力的 `Visit` 会直接返回，攻击力身上的运行期边要靠第 6 步。

例子：`Strength` 的静态闭包含 `AttackFromStrength`，另外有一条运行期边 `Strength → Attack`，`Attack` 的静态闭包含 `BonusFromAttack`。

```text
Visit(Strength)
    标脏 Strength
    闭包标脏 AttackFromStrength、DamageFromAttack
    运行期边 Visit(Attack)
        标脏 Attack
        闭包标脏 BonusFromAttack
        再走 Attack 和 BonusFromAttack 的运行期边
    再走 AttackFromStrength、DamageFromAttack 的运行期边
```

遍历代号用 `int` 戳，配 `int[]`，按下标 `RawId` 记录本轮戳。戳回到 `int.MaxValue` 时绕回 1。数组和用于收集持有者的缓冲放在 Context 上复用。`SetBase` 之后的失效路径不要新分配 `HashSet` 或 `List`。`AddModifier` 时的字典增长可以分配，那不是每帧路径。

`OnAttributeValueChanged` 删除。分组事件只向外转发 `AttributeChanged`。注释要写明：标脏若留在这里，`SetBase(Strength, 80)` 之后不读力量就读 `DamageFromAttack`，伤害会一直是 210。

## 6. 静态闭包

在 `BuildDependencyGraphOrThrow` 的直接反向图完成之后生成。直接反向图 `_dependents` 保留，供诊断。新增：

```csharp
IReadOnlyList<AttributeId> GetDownstream(AttributeId id)
```

加到 `IAttributeRegistry` 和 `AttributeRegistry`。未知 id、没有下游时返回空列表，不要返回 null。

构建：

- 从每个 id 的直接下游出发做 DFS 或 BFS。
- 钻石依赖只保留一次。`Strength → Attack`、`Strength → Crit`、两者都 → `Damage` 时，`Strength` 的闭包里 `Damage` 只出现一次。
- 不要包含自己。
- 静态图在冻结时已经拒绝自依赖和环，这里不必再检测环。
- 下游 `AttributeId` 带上注册表里的名字：`new AttributeId(rawId, node.Name)`。
- 结果存成 `AttributeId[]`，冻结后不再修改。
- `Freeze()` 再次进入 `BuildDependencyGraphOrThrow` 时，闭包和直接图一起重建。

公式依赖和 `DependsOn` 可能重复指向同一个属性。闭包去重之后，`AttackFromStrength` 只会在 `Strength` 的下游里出现一次。

## 7. 运行期索引

新建 `AbilityKit.Attributes.Core.AttributeInvalidationIndex`，由每个 `AttributeContext` 持有。不要放进注册表。

两张表：

```text
运行期反向边：sourceRawId → (holderRawId → 引用计数)
上下文订阅：  inputKey → (holderRawId → 引用计数)
```

键用 `StringComparer.Ordinal`。计数到 0 就删除条目。

从一条 `ModifierData` 收集依赖，单独做一个静态方法，例如 `ModifierDependencyNotes.Collect`。返回两类结果：引用到的源属性 `RawId`，以及上下文键。

| 来源 | 登记 |
| --- | --- |
| `Fixed` | 无 |
| `Scalable` | 键 `"level"` |
| `Attribute` | 源属性 `AttributeKey.Packed`，0 则忽略 |
| `TimeDecay` | 键 `"elapsedTime"` |
| `ContextFloat` | `ContextFloatKey`，空字符串则忽略 |
| `Pipeline` | 看下面的步骤，不把 Pipeline 本身当一个键 |
| `SkillLevelParameter`、`HeroLevelSkillParameter` | 无 |

Pipeline 的 `MagnitudePipelineData.Count` 最多 4，步骤在 `Modifier0` 到 `Modifier3`。只读 `SingleModifierData`：

| TypeId | 类型 | 登记 |
| --- | --- | --- |
| 0 | Fixed | 无 |
| 1 | TimeDecay | `"elapsedTime"` |
| 2 | LevelCurve | `"level"` |
| 3 | AttributeRef | `Param0` 转成源 `RawId`，0 则忽略 |
| 4 | Stacking，当前序列化会落成 Fixed | 无 |
| 5 | Scale | 无 |

一条修饰器可以同时登记边和键。例如 Pipeline 里既有 `AttributeRef(Strength)` 又有 `LevelCurve`：反向边 `Strength → 持有者`，订阅 `"level" → 持有者`。

`AddModifier` 成功写入槽位后：先 `Add` 登记，再 `Invalidate(持有者)`。

`RemoveModifier` 只有在槽位确实停用后才 `Remove` 登记，然后 `Invalidate(持有者)`。停用失败则两件事都不做。登记使用槽里仍然保留的 `ModifierData`。

`ClearModifiers`：

- 没有任何活跃修饰器：直接返回。
- `sourceId == 0`：注销全部活跃修饰器。
- 其它 `sourceId`：只注销本次真正停用的修饰器。
- 至少停用了一条，才 `Invalidate(持有者)` 一次。按 `sourceId` 清除但没有命中时，不标脏。这是有意的行为变化。现在的实现只要存在别的活跃修饰器就会 `MarkDirty`。

注销和失效的对象是持有者。反向边是「源 → 持有者」。卸掉攻击力上的力量修饰器时，要脏的是攻击力和它的下游，不是力量。

## 8. 各写入点

都改为调用第 5 节的入口。属性实例通过已有的 `_ctx` 调用。

| 位置 | 做什么 |
| --- | --- |
| `AttributeInstance.BaseValue` set | 值变化后写 `BaseValue`，再 `Invalidate(自己)`。不要只设 `slot.Dirty` |
| `AttributeInstance.AddModifier` | 登记，再 `Invalidate(自己)` |
| `AttributeInstance.RemoveModifier` | 停用成功后注销，再 `Invalidate(自己)` |
| `AttributeInstance.ClearModifiers` | 按第 7 节注销，有停用才 `Invalidate(自己)` |
| `AttributeContext.Level` set | 变化后 `InvalidateInput("level")` |
| `AttributeContext.ElapsedTime` set | 变化后 `InvalidateInput("elapsedTime")` |
| `AttributeContext.SetFloat` / `SetInt` | 值变化后 `InvalidateInput(key)`，替换现在的全表扫描 |
| `AttributeContext.CurrentTime` / `DeltaTime` | 不改 |
| `SetData<T>` | 不改。它不参与修饰器数值 |
| 分组上的 `AttributeChanged` 订阅 | 只转发外部事件 |

`IModifierContext.Level` 和 `ElapsedTime` 继续返回这两个属性。对象初始化器 `new AttributeContext { Level = level }` 会走 setter。那时还没有修饰器，订阅表是空的，失效是空操作。

`AttributeGroup.MarkDirty` 保持「只标已有实例」。Context 侧先按 `Registry.GetGroup(id) ?? ""` 查找 `_groups`，没有分组就返回。不要调用会创建分组的 `GetGroupFor`。

`MarkDirtyByDependency` 继续负责 `slot.Dirty = true` 和 `_calculator.Invalidate()`。失效入口标的是它，不是外面再设一次 `Dirty`。

`MarkDirtyIfContextFloatDependent` 和 `MarkContextFloatDependentsDirty` 删除，改由订阅表取代。若还留着扫描，`SetFloat` 会和订阅表各做一遍，下游语义也仍然不完整。

## 9. 注释原文

注释用中文。标识符保持英文。下面几段放到对应实现旁边，标签保持原样，方便搜索。

### 9.1 `AttributeInvalidationIndex` 类型声明上方

```csharp
/// <summary>
/// [AK-INVALIDATE] 一个属性上下文上的失效索引。
/// 静态公式由注册表闭包处理。这里只记录战斗中才出现的两类关系：
/// 修饰器引用了哪个属性，以及修饰器读了哪个上下文输入。
///
/// 静态闭包例子：Strength 从 10 改为 80 后，不读 Strength。
/// AttackFromStrength 的公式是 base + Strength * 0.5，DamageFromAttack = AttackFromStrength * 2。
/// 闭包使三者都变脏，只读 DamageFromAttack 得到 280，而不是缓存里的 210。
///
/// 运行期反向边例子：Attack 基础值 0，修饰器为 Strength * 0.5。
/// 力量 10 时攻击力是 5；力量改为 20 后，不读力量，攻击力下次读取是 10。
/// 依赖攻击力的 BonusFromAttack 同时变脏，下次读取是 20。
///
/// 上下文输入例子：不要为每种来源写 xxxSensitive。
/// "level" 上的 LevelCurve(10, {1,1, 3,2}) 在等级 1→3 时从 10 变为 20。
/// "elapsedTime" 上的线性 TimeDecay(80, 4) 在时间 0→2 时从 80 变为 40。
/// 以后的日期来源只追加键 "day"，由 Day 的写入调用 InvalidateInput("day")。
/// </summary>
```

### 9.2 `AttributeRegistry` 闭包字段

```csharp
/// <summary>
/// [AK-INVALIDATE] 冻结后不变的传递闭包，值是全部静态下游，不含自己。
/// 例子：Strength 的下游含 AttackFromStrength 和 DamageFromAttack。
/// SetBase(Strength, 80) 时两者一起标脏，读取才重算。
/// </summary>
```

### 9.3 `Visit` 方法

```csharp
// [AK-INVALIDATE] 只标脏，不重算。
// 先标自己和静态闭包，再沿运行期边继续。
// 闭包里的节点即使已经访问过，仍要走它的运行期边：
// Strength 的闭包含 AttackFromStrength 时，Attack 上的 Attribute(Strength) 边不在闭包数组里。
// 已访问的节点直接返回，截断 Strength ↔ Attack 这类运行期环。
```

### 9.4 删除 `OnAttributeValueChanged` 的位置

```csharp
// [AK-INVALIDATE] 不在 AttributeChanged 里标脏。
// Changed 只有读取并重算之后才会发生。
// 若标脏留在这里，SetBase(Strength, 80) 后不读力量就读 DamageFromAttack，结果会停留在 210。
```

### 9.5 `BaseValue` setter

```csharp
// [AK-INVALIDATE] 基础值变化后失效自己和全部下游。
// 例子：MaxHealth 800→500 后，不读 MaxHealth，HealthFromMaxHealth 下次读取为 500。
```

### 9.6 `AddModifier` 登记处

```csharp
// [AK-INVALIDATE] 先登记再失效。
// Attribute(Strength, 0.5) 记下 Strength → 本属性。
// LevelCurve 订阅 "level"，TimeDecay 订阅 "elapsedTime"，ContextFloat 订阅它的 key。
// 两个 Buff 引用同一力量时计数为 2，卸光才删边。
```

### 9.7 `Level` / `ElapsedTime` setter

```csharp
// [AK-INVALIDATE] 等级只失效订阅了 "level" 的属性，再带上它们的下游。
// LevelProbe 使用 LevelCurve(10, {1,1, 3,2})：等级 1 读到 10，等级 3 读到 20。
// TimeProbe 不在这张表里，不会变脏。
```

```csharp
// [AK-INVALIDATE] 已过时间只失效订阅了 "elapsedTime" 的属性。
// 线性 TimeDecay(80, 4)：ElapsedTime 0 读到 80，改为 2 后读到 40。
// CurrentTime 不参与这条公式，不在这里失效。
```

### 9.8 `SetFloat`

```csharp
// [AK-INVALIDATE] 按 key 失效持有者及其下游，不再扫描全部修饰器。
// Attack 使用 ContextFloat("buffPower")，BonusFromAttack = Attack * 2。
// SetFloat("buffPower", 5) 后不读 Attack，BonusFromAttack 下次读取为 10。
// 新增日期这类输入时复用 InvalidateInput，不要新增 daySensitive 字段。
```

## 10. 课程验收

扩展 `Assets/AbilityKitLessons/Lesson05_Dependencies.cs`，不要新编号。`LessonRunner` 和 `LessonBatchValidation` 不用为新课程序号改数组。

新属性加在 `LessonCatalog.EnsureInitialized` 现有 `Register` 之后：

- `Lesson.DamageFromAttack`
- `Lesson.BonusFromAttack`
- `Lesson.LevelProbe`
- `Lesson.TimeProbe`

定义与第 4 节相同。不要改 `Lesson.Strength` 及它之前的注册顺序。

`Lesson05.Run` 按第 4 节的三段执行，并用 `throw new InvalidOperationException` 断言数值。异常信息里带 `[AK-INVALIDATE]`。批处理只把空输出和异常当成失败，所以必须抛异常，不能只写进返回文本。

返回文本用中文写清三段结果，供 Play 模式阅读。删掉文件里旧的注释块：那段注释记录过 `HealthFromMaxHealth` 在 `MaxHealth` 改为 500 后仍是 `800.00`。

同时更新 `Assets/AbilityKitLessons/ArchitectureGuide.md` 第 8 节。把「源属性变化后，Context 找到所有下游属性并标记 Dirty」写成：

- 冻结时建立静态传递闭包。
- 运行期修饰器引用另外登记反向边。
- 等级、已过时间和 `ContextFloat` 按输入键订阅。
- 写入时只标脏，下次读取再重算。
- 用力量 → 攻击力 → 伤害的 10 到 80、伤害 210 到 280 作为例子。

## 11. 不要做

- 不要在失效时调用 `GetValue` 或 `Recompute`。
- 不要用一个全局 epoch 让所有属性在等级或时间变化后做依赖校验。`ElapsedTime` 会频繁变化，攻击力不应因此每帧检查依赖。
- 不要要求调用方把 Buff 的 `MagnitudeSource.Attribute` 提前写进 `DependsOn`。边在 `AddModifier` 时登记。
- 不要把运行期边写进冻结闭包。闭包冻结后不可变，Buff 之后还会增删。
- 不要为 `CurrentTime`、`DeltaTime`、`SetData` 增加失效。
- 不要调用 `SingleModifierData.Modify` 来发现依赖。
- 不要改运算符优先级、公式求值顺序、约束应用顺序和课程 01 到 04、06 到 09 的既有断言。
- 不要编辑 `*.csproj` 或 `*.sln`。
- 不要用 `dotnet build` 验证。需要验证时关闭 Unity 编辑器后跑课程批处理。成功标准是退出码 0，且日志含 `[AbilityKit Lesson] All lessons passed.`：

```text
"C:\Program Files\Unity\Hub\Editor\Unity 6000.3.5f2\Editor\Unity.exe" -batchmode -quit -projectPath "E:\UnityProjects\AttributeAndModifier" -executeMethod AttributeAndModifier.Lessons.Editor.LessonBatchValidation.Run -logFile "E:\UnityProjects\AttributeAndModifier\Logs\lessons.log"
```

## 12. 实现顺序

1. 注册表生成去重的静态闭包，并加到 `IAttributeRegistry`。
2. 实现 `AttributeInvalidationIndex` 和 `ModifierDependencyNotes.Collect`。
3. 实现带遍历戳的 `Invalidate` / `InvalidateInput`，标脏时不创建分组或实例。
4. 把第 8 节的写入点接上，删除 `OnAttributeValueChanged` 的标脏和 `ContextFloat` 全表扫描。
5. 按第 9 节写上带例子的 `[AK-INVALIDATE]` 注释。
6. 扩展课程 05 和 `ArchitectureGuide.md` 第 8 节。
7. 跑第 11 节的批处理。编辑器占用工程锁时不要并行启动第二个 Unity。
