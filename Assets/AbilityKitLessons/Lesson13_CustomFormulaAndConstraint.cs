using System;
using AbilityKit.Attributes.Constraint;
using AbilityKit.Attributes.Core;
using AbilityKit.Attributes.Formula;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 自定义公式：把修改器结果限制在 MaxHealth 以内。
    /// 依赖通过 AttributeDef.DependsOn 声明，MaxHealth 变化时本属性会被标记为脏。
    /// </summary>
    internal sealed class CappedByMaxHealthFormula : IAttributeFormula
    {
        private readonly AttributeId _maxHealth;

        public CappedByMaxHealthFormula(AttributeId maxHealth)
        {
            _maxHealth = maxHealth;
        }

        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult modifierResult)
        {
            float value = modifierResult.FinalValue;
            float cap = ctx.GetValue(_maxHealth);
            return value < cap ? value : cap;
        }
    }

    /// <summary>
    /// 自定义约束：超过阈值后只按比例保留超出部分。
    /// 例：threshold=100、compression=0.5 时，300 → 100 + 200×0.5 = 200。
    /// </summary>
    internal sealed class SoftCapConstraint : IAttributeConstraint
    {
        private readonly float _threshold;
        private readonly float _compression;

        public SoftCapConstraint(float threshold, float compression)
        {
            _threshold = threshold;
            _compression = compression;
        }

        public float Apply(AttributeId id, float value)
        {
            if (value <= _threshold) return value;
            return _threshold + (value - _threshold) * _compression;
        }
    }

    /// <summary>
    /// 示例 13：自定义 IAttributeFormula 和 IAttributeConstraint。
    /// </summary>
    public sealed class Lesson13_CustomFormulaAndConstraint : LessonExampleBase
    {
        public override string Title => "13 - 自定义公式与约束";
        public override string Purpose => "理解 IAttributeFormula 负责组装最终值、IAttributeConstraint 负责兜底限制，以及它们如何通过 AttributeDef 参与依赖失效。";

        public override string Run()
        {
            var context = LessonCatalog.NewContext();

            // 自定义公式：min(修改器结果, MaxHealth)
            context.SetBase(LessonCatalog.MaxHealth, 1000f);
            context.AddModifier(
                LessonCatalog.CustomFormulaProbe,
                ModifierData.Add(LessonCatalog.CustomFormulaProbe.ModifierKey, 400f));

            var formulaBefore = context.GetValue(LessonCatalog.CustomFormulaProbe);
            AssertNear(formulaBefore, 400f, "MaxHealth=1000 时自定义公式");

            // MaxHealth 变化后，DependsOn 让 CustomFormulaProbe 变脏，公式重新读取上限。
            context.SetBase(LessonCatalog.MaxHealth, 300f);
            var formulaAfter = context.GetValue(LessonCatalog.CustomFormulaProbe);
            AssertNear(formulaAfter, 300f, "MaxHealth=300 时自定义公式");

            // 自定义约束：80 不受影响，300 被软上限压到 200。
            context.SetBase(LessonCatalog.CustomConstraintProbe, 80f);
            var constraintLow = context.GetValue(LessonCatalog.CustomConstraintProbe);
            AssertNear(constraintLow, 80f, "软上限以下的数值");

            context.SetBase(LessonCatalog.CustomConstraintProbe, 300f);
            var constraintHigh = context.GetValue(LessonCatalog.CustomConstraintProbe);
            AssertNear(constraintHigh, 200f, "软上限以上的数值");

            return Line($"自定义公式 min(400, MaxHealth)：MaxHealth 1000 → {formulaBefore:0.00}，改为 300 → {formulaAfter:0.00}") +
                   Line($"软上限 constraint(100, 0.5)：80 → {constraintLow:0.00}，300 → {constraintHigh:0.00}") +
                   Line("关键点: 公式决定“怎么算”，约束决定“允许落在哪”；两者都在 AttributeInstance.Recompute 中按公式→约束的顺序执行。");
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (Math.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"[Lesson13] {label} 期望 {expected:0.00}，实际 {actual:0.00}");
            }
        }
    }
}
