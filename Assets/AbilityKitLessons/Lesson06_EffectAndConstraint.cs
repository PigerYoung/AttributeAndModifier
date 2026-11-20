using AbilityKit.Attributes;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 06：AttributeEffect 和 Constraint。
    /// </summary>
    public sealed class Lesson06_EffectAndConstraint : LessonExampleBase
    {
        public override string Title => "06 - AttributeEffect 与数值约束";
        public override string Purpose => "理解多个属性修改可以作为一个 Effect 应用，并通过同一个 SourceId 整体移除；约束负责兜底。";

        public override string Run()
        {
            var context = LessonCatalog.NewContext();
            context.SetBase(LessonCatalog.MoveSpeed, 8f);
            context.SetBase(LessonCatalog.CritChance, 0.2f);

            // AttributeEffect 只是数据：一组“属性 + ModifierData”条目。
            // 它不持有 Context，也不计算数值，只影响列出的属性（MoveSpeed、CritChance），
            // 不会修改 Context 里的其它属性。条目可以跨组。
            var effect = new AttributeEffect
            (
                AttributeEffect.Entry.Add(LessonCatalog.MoveSpeed, 100f),
                AttributeEffect.Entry.Add(LessonCatalog.CritChance, 2f)
            );

            // ApplyEffect = 批量 AddModifier，并给这批修改分配同一个 SourceId（用于后面整体移除）。
            // 它只写属性槽位，不计算；真正的计算发生在下面的 GetValue：
            //   GetValue → AttributeInstance.Recompute
            //     → ModifierCalculator.Calculate（汇总该属性的全部活跃 Modifier）
            //     → IAttributeFormula.Evaluate
            //     → IAttributeConstraint.Apply
            // 例：MoveSpeed 8 + 100 = 108，再被 Range[0,20] 约束成 20；
            //     CritChance 0.2 + 2 = 2.2，再被 [0,1] 约束成 1。
            //
            // 对比：若直接 new ModifierCalculator().Calculate(new[] { Add(100) }, 8f, null)，
            // 只会得到 108，不写入任何属性，也不会应用约束。
            var effectSourceId = context.ApplyEffect(effect);
            var boostedSpeed = context.GetValue(LessonCatalog.MoveSpeed);
            var boostedCrit = context.GetValue(LessonCatalog.CritChance);
            // RemoveEffect 按共享 SourceId 批量移除这批修改器；下次读取时 MoveSpeed 回到基础值 8。
            context.RemoveEffect(effectSourceId);
            var restoredSpeed = context.GetValue(LessonCatalog.MoveSpeed);

            return Line($"Effect SourceId={effectSourceId}") +
                   Line($"MoveSpeed 原始=8，添加 100 后受上限约束={boostedSpeed:0.00}") +
                   Line($"CritChance 原始=0.2，添加 2 后受 [0,1] 约束={boostedCrit:0.00}") +
                   Line($"移除整个 Effect 后 MoveSpeed={restoredSpeed:0.00}");
        }
    }
}
