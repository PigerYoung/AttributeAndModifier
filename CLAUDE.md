# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目定位

这是一个 Unity 6000.3.5f2 的 AbilityKit Attribute / Modifier "学习实验室"。工程本身几乎没有 gameplay 代码：`Assets/AbilityKitLessons/` 放的是 9 个可运行课程，框架源码以本地包的形式放在 `Packages/com.abilitykit.*` 下。这些包是可编辑源码（不是 `Library/PackageCache` 里的副本），也是主要需要修改的代码。

工程没有单元测试工程，也没有独立的构建工具链。验证方式是让 Unity 编译并（或）运行课程的批处理入口。

## 常用命令

**交互式运行课程：** 用 Unity `6000.3.5f2` 打开工程，打开 `Assets/Scenes/SampleScene.unity`，点击 Play。`LessonRunner` 会通过 `RuntimeInitializeOnLoadMethod` 自动创建；按 01 → 09 的顺序点击课程即可。

**批处理验证全部课程**（需要先关闭 Unity 编辑器，否则工程锁会阻止启动）：

```bash
"C:\Program Files\Unity\Hub\Editor\Unity 6000.3.5f2\Editor\Unity.exe" \
  -batchmode -quit \
  -projectPath "E:\UnityProjects\AttributeAndModifier" \
  -executeMethod AttributeAndModifier.Lessons.Editor.LessonBatchValidation.Run \
  -logFile "E:\UnityProjects\AttributeAndModifier\Logs\lessons.log"
```

成功标准：退出码为 0，且日志中出现 `[AbilityKit Lesson] All lessons passed.`。`LessonBatchValidation.Run()` 内部会调用 `EditorApplication.Exit(0)`，所以即使不加 `-quit` 它也会自行结束批处理编辑器。

**仅编译检查：** 使用同样的命令但不带 `-executeMethod`；日志中出现 `Exiting without the bug reporter. Application will terminate with return code 0` 即成功。现有的 `Logs/abilitykit-lessons-compile*.log` 就是这样产生的。

不要执行 `dotnet build`，也不要编辑生成的 `*.csproj` / `AttributeAndModifier.sln` 文件——Unity 会重新生成它们。

## 程序集与包布局

本地包依赖关系（箭头表示“依赖于”）：

```text
com.abilitykit.deterministic
        ↑
com.abilitykit.core            (noEngineReferences；纯 C# 基础层)
        ↑                    ↑
com.abilitykit.core.unity     com.abilitykit.modifiers  (noEngineReferences)
                                  ↑
                           com.abilitykit.attributes
com.abilitykit.gameplaytags    (独立包，无依赖)
        ↑
com.abilitykit.modifiers
```

- 只有 `AbilityKit.Core.Unity`、`AbilityKit.Core.Editor` 和 GameplayTags 的编辑器程序集引用 Unity。
- `Packages/com.abilitykit.core/Documentation~/CoreBoundary.md` 是强制执行的架构契约：Unity、Collections、Burst、玩法逻辑或领域策略都不得进入基础程序集 `AbilityKit.Core`。往 Core 添加类型前先阅读该文档。
- `Assets/AbilityKitLessons/` 没有 asmdef，因此课程代码编译进 `Assembly-CSharp`；`Assets/AbilityKitLessons/Editor/` 编译进 `Assembly-CSharp-Editor`。仅编辑器使用的代码必须放在该目录（或新建的 Editor asmdef）中。

## 核心架构

### 属性计算管线

```text
AttributeContext.GetValue(AttributeId)
  → AttributeGroup.GetValue
  → AttributeInstance.Value
      Dirty? → Recompute
        → ModifierCalculator.Calculate(modifiers, baseValue, context)
          → OperatorComposer.Compose          // Override > Add > PercentAdd > Mul
          → ModifierResult
        → IAttributeFormula.Evaluate          // DefaultAttributeFormula 或 AttributeExpressionFormula
        → IAttributeConstraint.Apply          // 可选的范围约束
      → 缓存结果，清除 Dirty
```

各层职责：

- `AttributeRegistry`（`AttributeRegistry.DefaultRegistry`，通过静态门面 `AttrRegistry` 暴露）—— 名称 → `AttributeId`、默认基础值、公式、约束、依赖图。`AttributeId.Id` 是 1 起始的注册索引（0 表示无效）。
- `AttributeContext` —— 一个实体的属性运行时环境，实现 `IModifierContext`。保存分组、等级、时间字段、float/int/object 数据槽以及 `AttributeChanged` 事件。它本身不持有全局状态。
- `AttributeGroup` —— 按领域组织属性的组，保存 `AttributeInstance` 以及槽位缓存值。
- `AttributeInstance` —— 单个属性：基础值、按 handle 索引的修改器槽位（带空闲链表）、活跃修改器临时缓冲、自己的 `ModifierCalculator`、Dirty/缓存值。
- `ModifierCalculator` → `ModifierComputeCore` + `OperatorComposer` —— 纯粹的 `ModifierData[]` + 基础值计算，支持来源记录和缓存。
- `MagnitudeSource` —— 描述修改器数值*从哪里来*：`Fixed`、`LevelCurve`、`Attribute`、`TimeDecay`、`ContextFloat`、`Pipeline`。
- `IAttributeFormula` / `IAttributeConstraint` —— 业务公式与最终值约束，由注册表解析。

### Modifier 语义

- 操作及 `IModifierOperator.Priority`：`Override` 0（终止操作，遇到即短路）、`Add` 10、`PercentAdd` 15、`Mul` 20。公式：`FinalValue = override ? overrideValue : (Base + AddSum) * PercentProduct * MulProduct`。
- 注册到 `ModifierOperatorRegistry` 的自定义操作，如果优先级不属于 {10, 15, 20}，会按 `IsAdditive` 回退到加法桶或乘法桶。
- 当修改器数量 > 1 时，`OperatorComposer.Compose` 会复制并稳定排序，因此该路径**并非**零分配，尽管设计目标是零 GC。只有输入已经排好序时，`ComposeSorted` 才能避免排序。
- 修改器有两种标识：`AddModifier` 返回的 `int` handle（移除单条），以及 `SourceId`（通过 `ClearModifiers(sourceId)` / `RemoveEffect` 移除整组效果）。`AttributeEffect` + `AttributeContext.ApplyEffect` 会给所有条目分配同一个自动生成的 `SourceId`。
- `ModifierStacking` 和 `ModifierLifecycle` 是纯结构体，不负责存储。叠层列表和帧推进由业务层持有。`ModifierLifecycle.IsExpired(frame)` 使用 `frame > StartFrame + DurationFrames`（严格大于）。

### 注册表冻结、ID 与脏标记传播

- `AttrRegistry.Freeze()` 在进程生命周期内不可逆：它会构建依赖图、拒绝自依赖和循环依赖，之后拒绝所有 `Register` 调用，以及 `Request` 从未注册过的名称。`LessonCatalog.Freeze()` 会在 `LessonRunner.Awake` 和批处理验证中执行，因此所有属性必须在冻结前注册。在编辑器中，域重载会在 Play 会话之间重置它。
- 注册顺序决定属性 ID。`AttributeContext.FindAttributeIdByKey` 把 `ModifierKey.Packed` 当作 `AttributeId.Id`；由于 `Lesson.Strength` 最先注册，`LessonCatalog.StrengthKey` 固定为 `FromPacked(1u)`。新增属性时必须留意这个映射关系。
- 属性值变化会通过注册表的 dependents 标记下游属性为 Dirty。Context 浮点数据槽变化只会标记 `MagnitudeSourceType` 为 `ContextFloat` 且 key 匹配的修改器。
- `AttributeExpressionFormula` 在首次求值时通过 `AttributeRegistry.DefaultRegistry` 延迟解析属性名，而不是通过 Context 注入的注册表。被引用的属性必须存在于默认注册表；引用自身会抛异常。内置变量：`base`、`add`、`mul`、`override`、`hasOverride`；函数：`abs`、`min`、`max`、`clamp`；运算符：`+ - * /` 和括号。
- `ModifierCalculator` 的缓存键是修改器数量、内容哈希、基础值，以及（存在时变来源时）`context.CurrentTime`。直接使用计算器时，输入变化后必须调用 `Invalidate()`；`AttributeInstance` 会通过自己的 Dirty 标记自动完成失效。

## 优先阅读的文件

- `Packages/com.abilitykit.attributes/Runtime/Attributes/Core/AttributeContext.cs`
- `Packages/com.abilitykit.attributes/Runtime/Attributes/Core/AttributeInstance.cs`
- `Packages/com.abilitykit.attributes/Runtime/Attributes/Core/AttributeRegistry.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Data/ModifierData.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Engine/ModifierCalculator.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Engine/OperatorComposer.cs`
- `Packages/com.abilitykit.modifiers/Runtime/Core/Source/MagnitudeSource.cs`
- `Assets/AbilityKitLessons/ArchitectureGuide.md` 和 `Assets/AbilityKitLessons/README.md`

## 编码约定

- 包内命名空间使用 `AbilityKit.*`；课程代码使用 `AttributeAndModifier.Lessons`（编辑器代码使用 `AttributeAndModifier.Lessons.Editor`）。领域包不得声明 `AbilityKit.Core.*` 命名空间。
- XML 文档注释、课程输出和设计文档都使用中文。新增课程文本和面向用户的说明请保持中文；代码标识符保持英文。
- 数据类型采用 struct + 静态工厂方法（`ModifierData.Add`、`MagnitudeSource.TimeDecay` 等）；私有字段使用 `_camelCase`。
- `Assets/AbilityKitLessons/LessonExample.cs` 定义了 `ILessonExample` 契约（`Title`、`Purpose`、`Run()`）。新增课程必须同时加入 `LessonRunner` 和 `Editor/LessonBatchValidation.Run()`。
