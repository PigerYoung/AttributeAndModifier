using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 08：操作器注册、泛型 Handler 和 CustomModifierData。
    /// </summary>
    public sealed class Lesson08_Extensions : LessonExampleBase
    {
        public override string Title => "08 - 自定义操作与类型 Handler";
        public override string Purpose => "理解框架如何通过接口扩展新操作，并把同一套 ModifierData 用于 bool、enum 等其他类型。";

        public override string Run()
        {
            ModifierOperatorRegistry.Register(new SubtractOperator());
            var custom = new ModifierData
            {
                Key = ModifierKey.DamageBonus,
                Op = (ModifierOp)200,
                Magnitude = MagnitudeSource.Fixed(15f)
            };
            var numericValue = new NumericModifierHandler().Apply(100f, custom, null);

            var enabled = new BooleanModifierHandler().Apply(
                false,
                ModifierData.Custom(ModifierKey.StateInvincible, ModifierOp.Override, CustomModifierData.Int(1)),
                null);

            return Line($"注册自定义 Subtract 操作后，100-15={numericValue:0.00}") +
                   Line($"BooleanModifierHandler + CustomModifierData.Int(1) => {enabled}") +
                   Line("关键点: IModifierOperator 扩展运算，IModifierHandler<T> 扩展被修改值的类型。");
        }

        private sealed class SubtractOperator : IModifierOperator
        {
            public ModifierOp OpCode => (ModifierOp)200;
            public string Name => "Subtract";
            public int Priority => 10;
            public bool IsTerminal => false;
            public bool IsAdditive => true;

            public float Apply(float baseValue, float modifierValue) => baseValue - modifierValue;
            public float CalculateContribution(float baseValue, float modifierValue) => -modifierValue;
        }
    }
}
