namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 01：BaseValue、最终 Value 和 AttributeContext 的最小关系。
    /// </summary>
    public sealed class Lesson01_BasicAttribute : LessonExampleBase
    {
        public override string Title => "01 - 基础属性与基础值";
        public override string Purpose => "理解属性不是字段，而是由 Context 管理、可被统一计算的运行时对象。";

        public override string Run()
        {
            LessonCatalog.EnsureInitialized();
            var context = LessonCatalog.NewContext();
            var group = context.GetGroupFor(LessonCatalog.Attack);
            var attack = group.GetOrCreate(LessonCatalog.Attack);
            var before = attack.Value;
            attack.BaseValue = 120f;
            var after = context.GetValue(LessonCatalog.Attack);

            return Line($"初始最终值: {LessonCatalog.Format(before)}") +
                   Line($"设置 BaseValue=120 后: {LessonCatalog.Format(after)}") +
                   Line("关键点: Value 是计算结果，BaseValue 只是计算输入。");
        }
    }
}
