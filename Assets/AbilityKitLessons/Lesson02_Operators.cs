using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 02：Add、PercentAdd、Mul、Override 的内置组合。
    /// </summary>
    public sealed class Lesson02_Operators : LessonExampleBase
    {
        public override string Title => "02 - Modifier 操作与组合顺序";
        public override string Purpose => "理解同一个属性可以同时拥有多种修改方式，并由统一计算器合成。";

        public override string Run()
        {
            var modifiers = new[]
            {
                ModifierData.Add(ModifierKey.AttackPower, 50f),
                ModifierData.PercentAdd(ModifierKey.AttackPower, 0.2f),
                ModifierData.Mul(ModifierKey.AttackPower, 1.5f)
            };
            var result = new ModifierCalculator().Calculate(modifiers, 100f);

            return Line("Base=100, Add=50, PercentAdd=20%, Mul=1.5") +
                   Line($"最终值: {result.FinalValue:0.00}") +
                   Line("默认组合顺序: Override → Add → PercentAdd → Mul。");
        }
    }
}
