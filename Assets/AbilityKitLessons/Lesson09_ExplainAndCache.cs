using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 09：来源追踪、结果拆解、缓存与显式失效。
    /// </summary>
    public sealed class Lesson09_ExplainAndCache : LessonExampleBase
    {
        public override string Title => "09 - 来源追踪与缓存";
        public override string Purpose => "理解 ModifierResult 如何拆解贡献，Recorder 如何记录来源，以及何时需要 Invalidate。";

        public override string Run()
        {
            var modifiers = new[]
            {
                ModifierData.Add(ModifierKey.AttackPower, 25f, sourceId: 1001),
                ModifierData.PercentAdd(ModifierKey.AttackPower, 0.1f, sourceId: 1002),
                ModifierData.Mul(ModifierKey.AttackPower, 1.2f, sourceId: 1003)
            };
            // recorder 负责记录每个修改器的具体数据
            var recorder = new DefaultRecorder();
            var calculator = new ModifierCalculator();
            // ModifierResult 里存放的是最终结果（包括总结果和不同计算区间的结果）
            var result = calculator.Calculate(modifiers, 100f, recorder);
            var text = Line($"最终值={result.FinalValue:0.00}，AddSum={result.AddSum:0.00}，MulProduct={result.MulProduct:0.00}");
            for (var i = 0; i < recorder.Count; i++)
            {
                var entry = recorder.GetEntry(i);
                text += Line($"来源 {entry.SourceId}: {entry.Op}，贡献={entry.Contribution:0.00}");
            }

            calculator.Invalidate();
            var recomputed = calculator.Calculate(modifiers, 100f).FinalValue;
            return text + Line($"Invalidate 后重新计算={recomputed:0.00}") +
                   Line("关键点: 默认路径可缓存；需要解释来源时使用 Recorder，数据变化时显式失效缓存。");
        }
    }
}
