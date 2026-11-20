# AbilityKit Attribute / Modifier 学习实验室

这个目录把 AbilityKit 的 Attribute 和 Modifier 拆成由浅入深的 14 个小实验。先在 Unity 中把 `LessonRunner` 挂到 `SampleScene` 的空 GameObject 上，再点击 Play，按 01 到 14 的顺序运行。

脚本也会在场景加载后自动创建 `LessonRunner`，所以不手动挂载也可以直接 Play。

架构总览请阅读 [ArchitectureGuide.md](ArchitectureGuide.md)。

## 课程地图

| 示例 | 主要表达 |
|---|---|
| 01 | `BaseValue`、最终 `Value`、`AttributeContext` |
| 02 | `Add`、`PercentAdd`、`Mul`、默认组合顺序 |
| 03 | `Handle`、`SourceId`、单个和整组移除 |
| 04 | `MagnitudeSource` 的等级、属性、上下文、时间、Pipeline 来源 |
| 05 | `AttributeDef`、表达式公式、依赖图和 Dirty 传播 |
| 06 | `AttributeEffect` 批量应用与 `IAttributeConstraint` |
| 07 | `ModifierStacking` 与按帧判断过期的 `ModifierLifecycle` |
| 08 | `IModifierOperator`、`IModifierHandler<T>`、`CustomModifierData` |
| 09 | `ModifierResult`、Recorder、来源贡献、缓存和 `Invalidate` |
| 10 | `IComposerStrategy`：替换分桶和加成规则，通过 `ModifierCalculator` 注入 |
| 11 | `IModifierComposer`：`Chain` / `Parallel` / `Conditional` 组合器 |
| 12 | 注册顺序、`Freeze`、`AttributeId` 稳定性和多 `AttributeContext` 隔离 |
| 13 | 自定义 `IAttributeFormula` 和 `IAttributeConstraint` |
| 14 | 实时值（`MagnitudeSource.Attribute`）与快照值（时点捕获） |

## 对照源码

核心源码位于 `Packages/com.abilitykit.attributes` 和 `Packages/com.abilitykit.modifiers`。建议边运行边阅读：

1. `attributes/Runtime/Attributes/Core/AttributeInstance.cs`
2. `modifiers/Runtime/Core/Engine/ModifierCalculator.cs`
3. `modifiers/Runtime/Core/Engine/OperatorComposer.cs`
4. `modifiers/Runtime/Core/Source/MagnitudeSource.cs`
5. `attributes/Runtime/Attributes/Core/AttributeContext.cs`

## 注意

本课程使用 AbilityKit 仓库中的核心包，不依赖完整 MOBA Demo。MOBA 的 Buff → Continuous → Attribute 投影方式可以在理解 01 到 14 后，再加入 `com.abilitykit.continuous` 和 `com.abilitykit.demo.moba.runtime` 学习。
