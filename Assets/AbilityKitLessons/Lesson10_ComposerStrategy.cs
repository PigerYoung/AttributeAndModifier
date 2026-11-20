using System;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 10：自定义组合策略 IComposerStrategy。
    /// </summary>
    public sealed class Lesson10_ComposerStrategy : LessonExampleBase
    {
        public override string Title => "10 - 自定义组合策略";
        public override string Purpose => "理解 IComposerStrategy 如何改变修改器的分桶和加成规则；策略通过 ModifierCalculator 构造器注入。";

        public override string Run()
        {
            var modifiers = new[]
            {
                ModifierData.Add(ModifierKey.AttackPower, 30f),
                ModifierData.PercentAdd(ModifierKey.AttackPower, 0.2f),
                ModifierData.Mul(ModifierKey.AttackPower, 1.5f)
            };

            // 默认策略：(Base + AddSum) * PercentProduct * MulProduct
            var defaultResult = new ModifierCalculator().Calculate(modifiers, 100f);

            // 自定义策略：PVP 规则下，加法类加成减半，百分比和乘法保持不变。
            var pvpResult = new ModifierCalculator(new PvpAddHalfStrategy()).Calculate(modifiers, 100f);

            AssertNear(defaultResult.FinalValue, 234f, "默认策略");
            AssertNear(pvpResult.FinalValue, 207f, "PVP 策略");

            return Line($"默认策略：(100 + 30) × 1.2 × 1.5 = {defaultResult.FinalValue:0.00}") +
                   Line($"PvpAddHalfStrategy：(100 + 30×0.5) × 1.2 × 1.5 = {pvpResult.FinalValue:0.00}") +
                   Line("关键点: 策略改变分桶和桶内数值；FinalValue 公式不变；AttributeInstance 目前固定使用默认策略。");
        }

        /// <summary>
        /// 示例策略：PVP 场景下加法类贡献减半。
        /// </summary>
        private sealed class PvpAddHalfStrategy : IComposerStrategy
        {
            public string Name => "PvpAddHalf";

            public ModifierResult Compose(
                ReadOnlySpan<ModifierData> modifiers,
                float baseValue,
                float level,
                IModifierContext context)
            {
                float addSum = 0f;
                float percentProduct = 1f;
                float mulProduct = 1f;
                int appliedCount = 0;

                for (int i = 0; i < modifiers.Length; i++)
                {
                    var modifier = modifiers[i];
                    float value = modifier.GetMagnitude(level, context);
                    var op = ModifierOperatorRegistry.Get(modifier.Op);
                    if (op == null) continue;

                    // Override 仍然是终止操作。
                    if (op.IsTerminal)
                    {
                        return new ModifierResult
                        {
                            BaseValue = baseValue,
                            PercentProduct = 1f,
                            MulProduct = 1f,
                            OverrideValue = value,
                            OverrideFlag = 1,
                            Count = 1
                        };
                    }

                    switch (op.Priority)
                    {
                        case 10:
                            addSum += value;
                            appliedCount++;
                            break;

                        case 15:
                            percentProduct *= 1f + value;
                            appliedCount++;
                            break;

                        case 20:
                            mulProduct *= value;
                            appliedCount++;
                            break;

                        default:
                            if (op.IsAdditive) addSum += value;
                            else mulProduct *= value;
                            appliedCount++;
                            break;
                    }
                }

                // 这条业务规则只存在于这个策略里，不需要改动任何 ModifierData。
                addSum *= 0.5f;

                return new ModifierResult
                {
                    BaseValue = baseValue,
                    AddSum = addSum,
                    PercentProduct = percentProduct,
                    MulProduct = mulProduct,
                    OverrideFlag = 0,
                    Count = appliedCount
                };
            }
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (Math.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"[Lesson10] {label} 期望 {expected:0.00}，实际 {actual:0.00}");
            }
        }
    }
}
