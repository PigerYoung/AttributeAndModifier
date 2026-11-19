using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 03：句柄和来源生命周期。
    /// </summary>
    public sealed class Lesson03_Lifetime : LessonExampleBase
    {
        public override string Title => "03 - Handle、SourceId 与移除";
        public override string Purpose => "理解装备、Buff 等来源如何独立移除，不需要手动反向计算数值。";

        public override string Run()
        {
            var context = LessonCatalog.NewContext();
            var combatGroup = context.GetGroupFor(LessonCatalog.Attack);
            combatGroup.SetBase(LessonCatalog.Attack, 100f);
           
            var swordHandle = combatGroup.AddModifier(LessonCatalog.Attack, ModifierData.Add(ModifierKey.AttackPower, 30f, sourceId: 101));
            combatGroup.AddModifier(LessonCatalog.Attack, ModifierData.PercentAdd(ModifierKey.Defense, 0.2f, sourceId: 202));
           
            var equipped = combatGroup.GetValue(LessonCatalog.Attack);
            
            combatGroup.RemoveModifier(LessonCatalog.Attack, swordHandle);
            var afterSword = combatGroup.GetValue(LessonCatalog.Attack);
            
            combatGroup.ClearModifiers(202);
            var afterBuff = combatGroup.GetValue(LessonCatalog.Attack);

            return Line($"装备和 Buff 同时存在: {equipped:0.00}") +
                   Line($"按 Handle 移除装备: {afterSword:0.00}") +
                   Line($"按 SourceId=202 移除 Buff: {afterBuff:0.00}") +
                   Line("关键点: SourceId 让一组跨属性 Modifier 具备统一生命周期。");
            
            // 装备和 Buff 同时存在: 156.00
            // 按 Handle 移除装备: 120.00
            // 按 SourceId=202 移除 Buff: 100.00
            // 关键点: SourceId 让一组跨属性 Modifier 具备统一生命周期。
        }
    }
}
