using System;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 07：ModifierStacking 和 ModifierLifecycle。
    /// </summary>
    public sealed class Lesson07_Stacking : LessonExampleBase
    {
        public override string Title => "07 - 叠层与临时生命周期";
        public override string Purpose => "理解叠层存储策略与数值计算分离，以及 Modifier 如何按帧判断过期。";

        public override string Run()
        {
            var stack = ModifierStacking.CreateAggregate(
                ModifierKey.DamageBonus,
                maxStack: 3,
                initialCount: 1,
                entry: ModifierData.Add(ModifierKey.DamageBonus, 10f));
            var pushedSecond = stack.TryPush(ModifierData.Add(ModifierKey.DamageBonus, 10f));
            var pushedThird = stack.TryPush(ModifierData.Add(ModifierKey.DamageBonus, 10f));
            var pushedFourth = stack.TryPush(ModifierData.Add(ModifierKey.DamageBonus, 10f));
            var expanded = new ModifierData[3];
            var expandedCount = stack.ExpandTo(expanded);
            var stackedValue = stack.CalculateStackedValue(100f);

            var lifecycle = ModifierLifecycle.Temporary(startFrame: 10, durationFrames: 3);
            return Line($"叠层 Push 结果: {pushedSecond}/{pushedThird}/{pushedFourth}") +
                   Line($"当前层数={stack.StackCount}，ExpandTo 条目数={expandedCount}，100+10*层数={stackedValue:0.00}") +
                   Line($"临时 Modifier 在 frame=12 过期? {lifecycle.IsExpired(12)}") +
                   Line($"临时 Modifier 在 frame=13 过期? {lifecycle.IsExpired(13)}") +
                   Line("关键点: 业务层决定如何存储叠层，核心结构提供展开和快速计算能力。");
        }
    }
}
