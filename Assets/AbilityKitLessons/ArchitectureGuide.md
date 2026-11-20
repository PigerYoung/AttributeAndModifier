# AbilityKit Attribute / Modifier 架构说明书

## 1. 这套架构解决什么问题

战斗系统里最容易失控的部分，通常不是“如何把 100 变成 120”，而是：

- 这个 20 来自装备、Buff，还是技能临时效果？
- Buff 结束时如何准确撤销，而不影响其他来源？
- 加法、百分比、乘法和覆盖同时存在时，顺序是什么？
- 攻击力变化后，伤害、护盾或最大生命是否需要重新计算？
- 属性值被频繁读取时，如何避免每次都遍历所有 Buff？

AbilityKit 的思路是把“属性是什么”和“属性如何被修改”拆开：

```text
Attribute = 被计算的对象
Modifier  = 对属性施加的一条规则
Formula   = 属性最终值的业务公式
Constraint = 属性值的边界规则
Context   = 一个角色或实体的运行时属性环境
```

外部系统不直接写 `attack += 50`，而是向属性系统添加一条 Modifier。这样装备、Buff、技能和临时状态都可以使用同一套机制。

## 2. 总体分层

```text
业务层：装备、Buff、技能、角色模板
    ↓ 创建 ModifierData / AttributeEffect
属性层：AttributeContext → AttributeGroup → AttributeInstance
    ↓ 调用
计算层：ModifierCalculator → OperatorComposer → ModifierResult
    ↓ 交给
公式层：IAttributeFormula
    ↓ 最后执行
约束层：IAttributeConstraint
```

每一层只负责一件主要事情：

| 层 | 主要职责 | 为什么独立 |
|---|---|---|
| `AttributeRegistry` | 注册属性定义、分组、默认值和依赖关系 | 属性目录不应该散落在角色代码里 |
| `AttributeContext` | 保存一个实体的属性运行时环境 | 不同实体之间的属性状态互不污染 |
| `AttributeGroup` | 组织一组属性 | 支持按 Combat、Resource 等领域分组 |
| `AttributeInstance` | 保存一个属性的基础值、Modifier 和缓存值 | 单个属性可以独立失效和重算 |
| `ModifierData` | 描述一次修改 | 装备、Buff、技能可以共享同一种数据结构 |
| `ModifierCalculator` | 计算 Modifier 的结果 | 计算逻辑集中，便于测试和优化 |
| `IAttributeFormula` | 根据计算结果得到业务最终值 | 不同属性可以拥有不同公式 |
| `IAttributeConstraint` | 限制最终值范围 | 生命、概率、速度等规则互不耦合 |

## 3. Attribute 是什么

### 3.1 `AttributeDef`：属性的规格说明书

`AttributeDef` 描述“这个属性应该如何工作”，不是某个角色当前拥有的数值。

它可以包含：

- 名称，例如 `Lesson.Attack`
- 分组，例如 `Combat`
- 默认基础值，例如 `100`
- 公式，例如 `base + add + Strength * 0.5`
- 约束，例如 `[0, 1]`
- 依赖属性，例如 `AttackFromStrength` 依赖 `Strength`

类比一下：`AttributeDef` 像数据库表结构，`AttributeInstance` 像某个角色在这张表中的一行数据。

### 3.2 `AttributeId`：稳定的属性标识

运行时不应该到处传递字符串：

```csharp
context.GetValue("Attack");
```

而是先注册并保存 `AttributeId`：

```csharp
var attackId = AttrRegistry.Attr("Lesson.Attack");
var attack = context.GetValue(attackId);
```

这样做的原因是：

1. 属性名称只在注册阶段解析。
2. 运行时用稳定 ID 访问，减少字符串比较。
3. 依赖图和缓存可以用 ID 建立关系。

`RawId` 由注册顺序决定：在中间插入新属性会让后面的 ID 整体后移，因此存档和网络协议不应依赖绝对 ID。注册表冻结后 `Register` 和未注册名称的 `Request` 都会抛异常。课程 12 演示了注册顺序、冻结行为，以及同一套 `AttributeDef` 在多个 `AttributeContext` 上的独立数值。

## 4. Modifier 是什么

一个 Modifier 不是简单的“加 10”，而是一组完整描述：

```text
目标：修改哪个属性
操作：Add / Mul / PercentAdd / Override
数值：修改多少
优先级：什么时候参与计算
来源：谁添加的
来源类型：固定值、等级曲线、属性引用、时间衰减等
```

对应到 `ModifierData`，重点字段是：

- `Key`：修改目标的稳定键。
- `Op`：修改操作。
- `Priority`：自定义排序优先级。
- `SourceId`：来源身份。
- `Magnitude`：修改数值的来源。
- `Metadata`：来源名称、标签、Owner 等附加信息。
- `CustomData`：自定义操作所需的数据。

### 4.1 为什么不直接修改最终值

假设攻击力初始是 100：

```text
装备：+30
Buff：+20%
技能状态：×1.5
```

如果直接修改字段，代码可能变成：

```csharp
attack += 30;
attack *= 1.2f;
attack *= 1.5f;
```

当 Buff 结束时，系统必须知道当时的顺序、原始数值和其他修改，才能正确反向恢复。这很容易因为顺序变化或多个来源叠加而出错。

Modifier 的做法是保留事实：

```text
基础值 100
├── 装备 Modifier：Add 30
├── Buff Modifier：PercentAdd 0.2
└── 状态 Modifier：Mul 1.5
```

读取最终值时重新组合；移除 Buff 时只删除 Buff 对应的 Modifier，其他来源无需改变。

## 5. 一次属性计算是怎样发生的

以攻击力为例：

```text
context.GetValue(AttackId)
    ↓
AttributeGroup.GetValue
    ↓
AttributeInstance.Value
    ↓ Dirty 时调用 Recompute
ModifierCalculator.Calculate
    ↓
OperatorComposer 按优先级组合
    ↓
ModifierResult
    ↓
IAttributeFormula.Evaluate
    ↓
IAttributeConstraint.Apply
    ↓
返回最终值并缓存
```

默认内置操作通常按以下逻辑组合：

```text
Override：直接使用覆盖值
Add：基础值 + 所有 Add 总和
PercentAdd：乘以所有 (1 + 百分比)
Mul：乘以所有 Mul 值
```

例如：

```text
Base = 100
Add = 30
PercentAdd = 20%
Mul = 1.5

Final = (100 + 30) × 1.2 × 1.5 = 234
```

这里使用固定组合顺序，是为了让“装备 +30、Buff +20%、状态 ×1.5”在所有调用者看来都保持一致。若某个游戏需要不同规则，可以实现 `IComposerStrategy`，替换组合策略，而不是修改所有 Modifier。

## 6. `MagnitudeSource`：为什么把“操作”和“数值来源”分开

`ModifierData.Op` 只回答“怎么改”，`MagnitudeSource` 回答“改多少以及数值从哪里来”。

同样是 `Add`，它可以有完全不同的来源：

```csharp
ModifierData.Add(key, 50f);                         // 固定 +50
ModifierData.Add(key, levelSource);                 // 根据等级变化
ModifierData.Add(key, strengthSource);              // 等于力量的一半
ModifierData.Add(key, decaySource);                 // 随时间衰减
ModifierData.Add(key, pipelineSource);              // 多个来源串成管道
```

### 6.1 属性引用例子

设计一个“力量转攻击力”的效果：

```text
Strength = 80
Modifier = Add(Strength × 0.5)
攻击力增加 = 40
```

这里 Modifier 本身没有保存一个永久的 40，而是保存“读取 Strength，再乘以 0.5”的规则。Strength 从 80 变成 100 后，同一个 Modifier 的贡献就会变成 50。

这适合：

- 力量影响物理攻击。
- 智力影响法术强度。
- 最大生命影响护盾值。
- 武器伤害引用角色等级或熟练度。

### 6.2 快照值和实时值

有些效果应该在施加时固定，有些效果应该实时变化：

```text
快照：技能命中时按当时攻击力计算一次，之后攻击力变化不影响这次伤害。
实时：光环持续期间始终引用当前力量，力量变化会影响光环加成。
```

这两种语义必须在设计阶段明确，否则会出现“装备换下后，已经发射的火球伤害也变化”的问题。

课程 14 用同一组数字演示了两条路径：实时路径使用 `MagnitudeSource.Attribute`，力量变化后攻击力继续跟随；快照路径在“命中瞬间”把数值记录到伤害对象里，之后与源属性解耦。如果希望快照也走修改器，可以在捕获时使用 `MagnitudeSource.Fixed(capturedValue)`。

## 7. 脏标记和缓存

属性值不是每次读取都重新计算。`AttributeInstance` 会保存：

```text
BaseValue：基础值
Modifiers：当前有效 Modifier
Cached：上一次计算结果
Dirty：缓存是否失效
```

只有这些情况发生时才需要重新计算：

- 基础值改变。
- 添加或移除 Modifier。
- 被引用的属性改变。
- Context 中被引用的浮点数据改变。
- 外部主动调用计算器的 `Invalidate`。

例子：

```text
一帧内读取攻击力 20 次
第一次读取：Dirty，执行计算并缓存
后面 19 次：直接返回 Cached
添加一个 Buff：标记 Dirty
下一次读取：重新计算并更新 Cached
```

这样可以避免“100 个角色 × 50 个属性 × 每帧多次遍历 Buff”的重复开销。

## 8. 属性依赖为什么需要 Registry

假设：

```text
MaxHealth = 基础最大生命
Health = min(当前计算值, MaxHealth)
```

当 `MaxHealth` 改变时，`Health` 的缓存也必须失效。如果每个属性自己寻找依赖，系统会出现大量互相引用的代码。

AbilityKit 的做法是：

1. 注册属性定义时声明依赖。
2. `AttributeRegistry.Freeze()` 时建立依赖图，并生成静态传递闭包。
3. 冻结阶段检查循环依赖。
4. 运行期修饰器引用另外登记反向边。
5. 等级、已过时间和 `ContextFloat` 按输入键订阅。
6. 写入时只标脏，下次读取再重算。

例子：

```text
Strength 10 → 80
    ↓
静态闭包：AttackFromStrength、DamageFromAttack 一起变脏
    ↓
只读 DamageFromAttack
    ↓
DamageFromAttack 读 AttackFromStrength，AttackFromStrength 读 Strength
    ↓
210 → 280
```

静态闭包只覆盖 `DependsOn` 和公式里的属性。战斗中添加的修饰器（例如 `MagnitudeSource.Attribute(Strength, 0.5)`）在 `AddModifier` 时登记运行期反向边，卸下时按引用计数删除。等级、已过时间和 `ContextFloat` 走输入键订阅：`"level"`、`"elapsedTime"` 以及修饰器自己的 key。

冻结注册表的原因是：战斗开始后属性目录应该稳定。这样可以避免运行中途新增属性导致 ID、依赖图和缓存语义发生变化。

## 9. Formula 和 Constraint 的区别

这两个接口都参与最终值计算，但职责不同。

### Formula：决定怎么算

例如攻击力公式：

```text
Attack = Base + Add + Strength × 0.5
```

Formula 适合表达属性之间的业务关系、特殊计算和表达式。

### Constraint：决定允许落在哪个范围

例如暴击率：

```text
基础暴击率 = 0.2
Buff 增加 = 2.0
计算结果 = 2.2
约束 [0, 1]
最终结果 = 1.0
```

把边界逻辑放在 Constraint 中，可以避免每个装备、Buff、技能都重复写 `Clamp`。

课程 13 演示了自定义实现：`CappedByMaxHealthFormula` 在代码里读取 `MaxHealth` 并限制上限，`SoftCapConstraint` 对超过阈值的部分按比例压缩。公式和约束都通过 `AttributeDef` 注册；公式读取了哪个属性，就要在 `DependsOn` 中声明，才能被失效传播覆盖。

## 10. Effect、Handle 和 SourceId

### Handle：定位单条 Modifier

添加一条 Modifier 时返回 Handle：

```csharp
var handle = attribute.AddModifier(modifier);
attribute.RemoveModifier(handle);
```

适合移除某一件装备、某一次临时操作。

### SourceId：定位一组 Modifier

一个 Buff 往往同时影响多个属性：

```text
Buff “狂暴”
├── Attack +30
├── MoveSpeed +10%
└── CritChance +5%
```

这三条 Modifier 使用同一个 SourceId。Buff 结束时：

```csharp
context.ClearModifiers(buffSourceId);
```

系统就能一次清理整组效果。SourceId 本质上是“效果实例的生命周期身份”。

### AttributeEffect：批量描述一个效果

`AttributeEffect` 适合把多条属性修改组织成一个可应用对象：

```csharp
var rage = new AttributeEffect(
    AttributeEffect.Entry.Add(attackId, 30f),
    AttributeEffect.Entry.PercentAdd(moveSpeedId, 0.1f));

var sourceId = context.ApplyEffect(rage);
context.RemoveEffect(sourceId);
```

它让“创建效果”和“管理效果生命周期”分开，也方便未来从配置表生成 Effect。

## 11. 扩展点

### 11.1 新增操作

实现 `IModifierOperator` 并注册到 `ModifierOperatorRegistry`，可以增加例如：

- 最小值。
- 最大值。
- 减法。
- 按层数计算。
- 特殊抗性公式。

### 11.2 新增组合策略

实现 `IComposerStrategy`，可以替换 `ModifierCalculator` 内部的组合规则。默认策略是：

```text
FinalValue = (Base + AddSum) × PercentProduct × MulProduct
```

自定义策略仍然返回 `ModifierResult`，但可以改变分桶方式或桶内数值。例如 PVP 场景下把所有加法加成减半：

```text
默认：  (100 + 30) × 1.2 × 1.5 = 234
PVP：   (100 + 30 × 0.5) × 1.2 × 1.5 = 207
```

注入方式：

```csharp
var calculator = new ModifierCalculator(new PvpAddHalfStrategy());
var result = calculator.Calculate(modifiers, baseValue);
```

对应 `Lesson10_ComposerStrategy.cs`。注意 `AttributeInstance` 内部固定使用默认策略，目前只有直接使用 `ModifierCalculator` 时才能注入自定义策略。策略的产物仍是 `ModifierResult`，`FinalValue` 的公式固定作用于各桶；如果需要完全不同的最终公式，应换用 11.5 的 `IModifierComposer` 或直接计算。

### 11.3 新增数值来源

通过 `IMagnitudeModifier` 或 `MagnitudePipeline` 可以实现：

- 当前生命百分比。
- 距离目标的距离。
- 当前连击数。
- 资源值转化。
- 多个来源按顺序变换。

### 11.4 新增值类型 Handler

`IModifierHandler<T>` 不只适用于 `float`：

- `NumericModifierHandler`：数值。
- `BooleanModifierHandler`：布尔状态。
- `EnumModifierHandler<T>`：枚举状态。
- 自定义 Handler：项目自己的结构体或对象。

这说明 Modifier 系统不是“只能加攻击力的工具”，而是一个可扩展的值变换框架。

### 11.5 组合器 Chain / Parallel / Conditional

`IModifierComposer` 是另一层组合 API，操作对象是 `IMagnitudeModifier`，不涉及 `ModifierData`：

```text
ChainComposer        base → m0 → m1 → ...          前一个输出作为后一个输入
ParallelComposer     base + m0(0) + m1(0) + ...    每个修饰器从 0 计算贡献再相加
ConditionalComposer  满足条件时选一个修饰器，否则用默认修饰器
```

例子（`Lesson11_ModifierComposer.cs`）：

```text
Chain：              100 → +10 → ×2 = 220
Parallel（纯加法）：  100 + 10 + 5 = 115
Parallel（含 Scale）：100 + 10 + 0×2 = 110
Conditional：        等级 1 用 ×2 = 200，等级 3 用 ×1.5 = 150
```

`Chain` 与 `Parallel` 的区别只在修饰器是否依赖输入值：

- 纯加法修饰器时两者结果相同，因为加法可结合：`Chain(100 → +10 → +5)` 也是 115。
- 依赖输入的修饰器（如 `ScaleModifier`）才会拉开差异：`Chain` 把上一步结果传给下一步，得到 220；`Parallel` 给每个修饰器的输入都是 0，`Scale` 的贡献变成 `0 × 2 = 0`，结果是 110。
- `TimeDecay` / `LevelCurve` / `AttributeRef` 会忽略输入值：在 `Chain` 里它们会覆盖前一步的累积值，在 `Parallel` 里则作为独立贡献加到基础值上。

创建方式：

```csharp
var chain = ModifierComposerFactory.Chain(new AddMagnitudeModifier(10f), new ScaleModifier(2f));
var parallel = ModifierComposerFactory.Parallel(new AddMagnitudeModifier(10f), new AddMagnitudeModifier(5f));
var conditional = ModifierComposerFactory.When(ctx => ctx.Level >= 3f, new ScaleModifier(1.5f), new ScaleModifier(2f));
```

这条路径直接返回 float，不经过 `ModifierResult`、缓存或脏标记，适合技能、子弹、预览等局部数值计算，不是属性管线的一部分。

注意 `ModifierPipeline` 不是 `IModifierComposer`：它本身是一个 `IMagnitudeModifier`（内部也是链式执行），Lesson04 已经把它当作 `MagnitudeSource` 的来源使用。

## 12. 一个完整战斗例子

以“护盾光环”为例：

```text
业务层创建 Buff
    ↓
Buff 配置声明持续时间、层数和 Modifier
    ↓
Buff 生命周期创建一个 SourceId
    ↓
Modifier 投影到目标的 AttributeContext
    ↓
ShieldMax 的 Modifier 引用施法者的 MagicAttack
    ↓
目标读取 ShieldMax
    ↓
ModifierCalculator 计算
    ↓
Formula 处理护盾特殊公式
    ↓
Constraint 限制护盾上下限
    ↓
Buff 到期，按 SourceId 清除 Modifier
```

这里每个系统只做自己的事情：Buff 负责“什么时候有效”，Modifier 负责“改多少”，Attribute 负责“保存和计算”，Formula 负责“业务公式”，Constraint 负责“边界”。

这正是架构扩展性强的原因：新增一种 Buff 通常只需要新增配置和 Modifier 组合，不需要修改属性核心；新增一种属性公式也不需要修改 Buff 生命周期。

## 13. 推荐阅读顺序

在本工程中建议按以下顺序对照代码：

1. `Lesson01_BasicAttribute.cs`：属性和基础值。
2. `Lesson02_Operators.cs`：Modifier 操作和组合。
3. `Lesson03_Lifetime.cs`：Handle、SourceId 和移除。
4. `Lesson04_MagnitudeSources.cs`：动态数值来源。
5. `Lesson05_Dependencies.cs`：依赖图和 Dirty 传播。
6. `Lesson06_EffectAndConstraint.cs`：Effect 和约束。
7. `Lesson07_Stacking.cs`：叠层和生命周期。
8. `Lesson08_Extensions.cs`：自定义操作和 Handler。
9. `Lesson09_ExplainAndCache.cs`：来源追踪和缓存。
10. `Lesson10_ComposerStrategy.cs`：自定义组合策略。
11. `Lesson11_ModifierComposer.cs`：Chain / Parallel / Conditional 组合器。
12. `Lesson12_RegistryAndContexts.cs`：注册顺序、冻结和多 Context 隔离。
13. `Lesson13_CustomFormulaAndConstraint.cs`：自定义公式和约束。
14. `Lesson14_SnapshotVsLive.cs`：实时值与快照值。

核心实现入口：

- `Packages/com.abilitykit.attributes/Runtime/Attributes/Core/AttributeContext.cs`
- `Packages/com.abilitykit.attributes/Runtime/Attributes/Core/AttributeInstance.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Data/ModifierData.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Engine/ModifierCalculator.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Engine/OperatorComposer.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Source/MagnitudeSource.cs`
