using System;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 11：IModifierComposer 的链式、并行和条件组合。
    /// </summary>
    public sealed class Lesson11_ModifierComposer : LessonExampleBase
    {
        public override string Title => "11 - 组合器 Chain / Parallel / Conditional";
        public override string Purpose => "理解 IModifierComposer 如何用 IMagnitudeModifier 组织链式、并行和条件三种数值计算流程。";

        public override string Run()
        {
            var context = LessonCatalog.NewContext(level: 1f);

            // Chain：result 依次传给下一个 Modify(context, result)。
            var chain = ModifierComposerFactory.Chain(
                new AddMagnitudeModifier(10f),
                new ScaleModifier(2f));
            var chainValue = chain.Compose(100f, context);

            // Parallel：每个修饰器都执行 Modify(context, 0)，再把结果加到基础值上。
            // 两个都是纯加法时，Chain 也会得到 115（加法可结合），单看这一组看不出 Parallel 的特点。
            var parallel = ModifierComposerFactory.Parallel(
                new AddMagnitudeModifier(10f),
                new AddMagnitudeModifier(5f));
            var parallelValue = parallel.Compose(100f, context);

            // 换成依赖输入的 ScaleModifier，差异才显现：
            // Chain：(100 + 10) × 2 = 220。
            // Parallel：100 + Add(0)=10 + Scale(0)=0×2=0 → 110。
            var parallelMixed = ModifierComposerFactory.Parallel(
                new AddMagnitudeModifier(10f),
                new ScaleModifier(2f));
            var parallelMixedValue = parallelMixed.Compose(100f, context);

            // 条件：等级 >= 3 时使用 ×1.5，否则使用默认的 ×2。
            var conditional = ModifierComposerFactory.When(
                ctx => ctx.Level >= 3f,
                new ScaleModifier(1.5f),
                new ScaleModifier(2f));

            var level1Value = conditional.Compose(100f, context);
            context.Level = 3f;
            var level3Value = conditional.Compose(100f, context);

            AssertNear(chainValue, 220f, "ChainComposer");
            AssertNear(parallelValue, 115f, "ParallelComposer 纯加法");
            AssertNear(parallelMixedValue, 110f, "ParallelComposer 含 Scale");
            AssertNear(level1Value, 200f, "ConditionalComposer 等级 1");
            AssertNear(level3Value, 150f, "ConditionalComposer 等级 3");

            return Line($"Chain：100 → +10 → ×2 = {chainValue:0.00}") +
                   Line($"Parallel(两个加法)：100 + 10 + 5 = {parallelValue:0.00}（Chain 同样得到 115，加法可结合）") +
                   Line($"Parallel(含 Scale)：100 + 10 + 0×2 = {parallelMixedValue:0.00}（Scale 收到输入 0，贡献为 0）") +
                   Line($"Conditional：等级 1 用默认 ×2 = {level1Value:0.00}，等级 3 用 ×1.5 = {level3Value:0.00}") +
                   Line("关键点: Chain 传递累积值，Parallel 让每个修饰器从 0 计算贡献再求和；差异只在依赖输入的修饰器上。");
        }

        /// <summary>
        /// 课程内自定义修饰器：在输入值上加一个固定值。
        /// 内置 Modifier 中 ScaleModifier 会使用输入值，TimeDecay / LevelCurve / AttributeRef 会忽略输入值。
        /// </summary>
        private readonly struct AddMagnitudeModifier : IMagnitudeModifier
        {
            private readonly float _value;

            public AddMagnitudeModifier(float value)
            {
                _value = value;
            }

            public byte ModifierTypeId => 200;
            public string Name => "Add";
            public float Modify(IModifierContext context, float input) => input + _value;
            public float GetBaseValue() => _value;
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (Math.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"[Lesson11] {label} 期望 {expected:0.00}，实际 {actual:0.00}");
            }
        }
    }
}
