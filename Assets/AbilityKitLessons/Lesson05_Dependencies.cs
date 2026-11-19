namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 05：属性依赖和脏标记传播。
    /// </summary>
    public sealed class Lesson05_Dependencies : LessonExampleBase
    {
        public override string Title => "05 - 属性依赖与自动重算";
        public override string Purpose => "理解公式属性依赖其他属性时，源属性变化会使依赖属性失效并重新计算。";

        public override string Run()
        {
            var context = LessonCatalog.NewContext();
            context.SetBase(LessonCatalog.MaxHealth, 800f);
            context.SetBase(LessonCatalog.HealthFromMaxHealth, 1000f);
            var before = context.GetValue(LessonCatalog.HealthFromMaxHealth);

            context.SetBase(LessonCatalog.MaxHealth, 500f);
            context.GetValue(LessonCatalog.MaxHealth);
            var after = context.GetValue(LessonCatalog.HealthFromMaxHealth);

            context.SetBase(LessonCatalog.Strength, 80f);
            context.SetBase(LessonCatalog.AttackFromStrength, 100f);
            var attack = context.GetValue(LessonCatalog.AttackFromStrength);

            return Line($"MaxHealth=800 时，HealthFromMaxHealth={before:0.00}") +
                   Line($"MaxHealth 改为 500 后，HealthFromMaxHealth={after:0.00}") +
                   Line($"Strength=80 时，AttackFromStrength={attack:0.00}") +
                   Line("关键点: Registry 保存依赖图，Context 在依赖变化时标记下游属性 Dirty。");
            
            
            // MaxHealth=800 时，HealthFromMaxHealth=800.00
            // MaxHealth 改为 500 后，HealthFromMaxHealth=800.00
            // Strength=80 时，AttackFromStrength=140.00
            // 关键点: Registry 保存依赖图，Context 在依赖变化时标记下游属性 Dirty。
            
            // MaxHealth=800 时，HealthFromMaxHealth=800.00
            // MaxHealth 改为 500 后，HealthFromMaxHealth=500.00
            // Strength=80 时，AttackFromStrength=140.00
            // 关键点: Registry 保存依赖图，Context 在依赖变化时标记下游属性 Dirty。
        }
    }
}
