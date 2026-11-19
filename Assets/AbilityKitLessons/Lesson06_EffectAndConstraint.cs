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

            var effect = new AttributeEffect(
                AttributeEffect.Entry.Add(LessonCatalog.MoveSpeed, 100f),
                AttributeEffect.Entry.Add(LessonCatalog.CritChance, 2f));
            var effectSourceId = context.ApplyEffect(effect);
            var boostedSpeed = context.GetValue(LessonCatalog.MoveSpeed);
            var boostedCrit = context.GetValue(LessonCatalog.CritChance);
            context.RemoveEffect(effectSourceId);
            var restoredSpeed = context.GetValue(LessonCatalog.MoveSpeed);

            return Line($"Effect SourceId={effectSourceId}") +
                   Line($"MoveSpeed 原始=8，添加 100 后受上限约束={boostedSpeed:0.00}") +
                   Line($"CritChance 原始=0.2，添加 2 后受 [0,1] 约束={boostedCrit:0.00}") +
                   Line($"移除整个 Effect 后 MoveSpeed={restoredSpeed:0.00}");
        }
    }
}
