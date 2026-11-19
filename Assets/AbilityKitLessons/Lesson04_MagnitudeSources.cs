using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 04：MagnitudeSource 的动态数值来源。
    /// </summary>
    public sealed class Lesson04_MagnitudeSources : LessonExampleBase
    {
        public override string Title => "04 - 动态 MagnitudeSource";
        public override string Purpose => "理解 Modifier 的数值可以来自等级曲线、其他属性、时间衰减、上下文数据和管道。";

        public override string Run()
        {
            var context = LessonCatalog.NewContext(level: 3f);
            context.SetFloat("Rage", 40f);
            context.SetBase(LessonCatalog.Strength, 60f);

            var calculator = new ModifierCalculator { EnableCache = false };
            var sources = new[]
            {
                MagnitudeSource.LevelCurve(10f, new[] {  1f, 1f, 3f, 2f }),//曲线格式要求 [等级0, 数值0, 等级1, 数值1, ...]
                MagnitudeSource.Attribute(LessonCatalog.StrengthKey, 0.5f),
                MagnitudeSource.ContextFloat("Rage", 0.25f),
                MagnitudeSource.TimeDecay(40f, 4f, DecayType.Linear),
                MagnitudeSource.Pipeline(new ModifierPipeline().ThenScale(2f).ThenAttributeRef(LessonCatalog.StrengthKey, 0.1f))
            };

            var text = Line("统一使用 MagnitudeSource.Calculate(level, context)：");
            for (var i = 0; i < sources.Length; i++)
            {
                var value = sources[i].Calculate(context.Level, context);
                text += Line($"来源 {i + 1} [{sources[i].Type}] = {value:0.00}");
            }

            var decayModifier = ModifierData.Add(ModifierKey.AttackPower, sources[3]);
            var atStart = calculator.Calculate(new[] { decayModifier }, 100f, context).FinalValue;
            context.ElapsedTime = 2f;
            var atHalf = calculator.Calculate(new[] { decayModifier }, 100f, context).FinalValue;
            return text + Line($"同一个时间衰减 Modifier：0 秒={atStart:0.00}，2 秒={atHalf:0.00}") +
                   Line("关键点: ModifierData 描述操作，MagnitudeSource 描述数值从哪里来。");
            
            // 统一使用 MagnitudeSource.Calculate(level, context)：
            // 来源 1 [Scalable] = 20.00
            // 来源 2 [Attribute] = 30.00
            // 来源 3 [ContextFloat] = 10.00
            // 来源 4 [TimeDecay] = 40.00
            // 来源 5 [Pipeline] = 6.00
            // 同一个时间衰减 Modifier：0 秒=140.00，2 秒=120.00
            // 关键点: ModifierData 描述操作，MagnitudeSource 描述数值从哪里来。
        }
    }
}
