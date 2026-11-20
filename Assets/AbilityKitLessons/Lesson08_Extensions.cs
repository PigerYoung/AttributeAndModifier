using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 08：操作器注册、泛型 Handler 和 CustomModifierData。
    /// </summary>
    public sealed class Lesson08_Extensions : LessonExampleBase
    {
        public override string Title => "08 - 自定义操作与类型 Handler";
        public override string Purpose => "理解框架如何通过接口扩展新操作，并把同一套 ModifierData 用于 bool、enum 等其他类型。" +
                                          "1.自定义数据修改器" +
                                          "2.ModifierCalculator.Calculate和Handler计算的区别" +
                                          "3.Custom的目前是计算非数值 适合状态、枚举、ID";

        public override string Run()
        {
            ModifierOperatorRegistry.Register(new SubtractOperator());
            // 创建方式一：new ModifierData + 对象初始化器。
            // 只填写写出来的字段，其余取结构体默认值：
            //   Magnitude = Fixed(15)，CustomData 为空，Priority = 0。
            // 数值放在 Magnitude 里，交给数值路径（NumericModifierHandler / 属性管线）使用。
            var custom = new ModifierData
            {
                Key = ModifierKey.DamageBonus,
                Op = (ModifierOp)200,
                Magnitude = MagnitudeSource.Fixed(15f)
            };

            // 两种计算模型的区别（这里刻意用 Handler，而不是 ModifierCalculator.Calculate）：
            //
            // NumericModifierHandler.Apply：
            //   只处理一条 ModifierData，顺序调用 op.Apply(currentValue, modifierValue)。
            //   100 应用 Subtract(15) 得到 85，自定义操作器的 Apply 会真正生效。
            //   自己循环调用就是顺序叠加，先后顺序会影响结果：
            //   100 → Mul 1.5 → Add 10 = 160。
            //
            // ModifierCalculator.Calculate：
            //   处理一整组 ModifierData，按 IModifierOperator.Priority 分桶汇总：
            //   FinalValue = (Base + AddSum) * PercentProduct * MulProduct；Override 终止。
            //   默认策略不调用 op.Apply，只看 Priority 和 IsAdditive。
            //   Subtract(15) 的 Priority=10、IsAdditive=true，会被当成加法：
            //   100 + 15 = 115，而不是 85。
            //   同一桶内的顺序不影响结果：Add 10 与 Mul 1.5 得到 165。
            //   它还负责排序、缓存和来源记录，AttributeInstance.Recompute 用的就是它。
            //
            // 结论：两者不是“单条版 / 批量版”的关系，而是“逐条 Apply”和“分桶汇总”两套组合语义。
            var numericValue = new NumericModifierHandler().Apply(100f, custom, null);

            // 创建方式二：ModifierData.Custom。
            // 它仍是同一个 ModifierData 类型，只是工厂方法换了默认值：
            //   Magnitude = Fixed(0)（工厂不设置数值），CustomData = Int(1)，Priority = 10，SourceNameIndex = -1。
            // CustomData 本身不定义运算，由消费方解释：
            //   BooleanModifierHandler 读 IntValue（非 0 为 true），Override 直接设为 true。
            //
            // 与上面 new ModifierData 的区别：
            //   带 Magnitude 的走数值路径；带 CustomData 的走非数值路径。
            //   若互换，BooleanModifierHandler 读到的 IntValue 是 0 → false；
            //   NumericModifierHandler 读到的 Magnitude 是 0 → Override 结果为 0。
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
