using System;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 05：属性依赖、下游失效和按需重算。
    /// </summary>
    public sealed class Lesson05_Dependencies : LessonExampleBase
    {
        public override string Title => "05 - 属性依赖与自动重算";
        public override string Purpose => "理解静态闭包、运行期反向边和上下文输入订阅如何在写入时标记下游为脏，并在读取时按需重算。";

        public override string Run()
        {
            var text = string.Empty;

            // 4.1 静态闭包：Strength → AttackFromStrength → DamageFromAttack
            var context = LessonCatalog.NewContext();
            context.SetBase(LessonCatalog.Strength, 10f);
            context.SetBase(LessonCatalog.AttackFromStrength, 100f);

            var damageBefore = context.GetValue(LessonCatalog.DamageFromAttack);
            AssertNear(damageBefore, 210f, "Strength=10 时 DamageFromAttack");

            context.SetBase(LessonCatalog.Strength, 80f);
            var damageAfter = context.GetValue(LessonCatalog.DamageFromAttack);
            AssertNear(damageAfter, 280f, "Strength=80 时 DamageFromAttack");

            text += Line($"Strength 10→80，中间不读 Strength 和 AttackFromStrength，只读 DamageFromAttack：{damageBefore:0.00} → {damageAfter:0.00}");

            context.SetBase(LessonCatalog.MaxHealth, 800f);
            context.SetBase(LessonCatalog.HealthFromMaxHealth, 1000f);
            var healthBefore = context.GetValue(LessonCatalog.HealthFromMaxHealth);
            AssertNear(healthBefore, 800f, "MaxHealth=800 时 HealthFromMaxHealth");

            // 不读 MaxHealth，闭包也要让 HealthFromMaxHealth 失效。
            context.SetBase(LessonCatalog.MaxHealth, 500f);
            var healthAfter = context.GetValue(LessonCatalog.HealthFromMaxHealth);
            AssertNear(healthAfter, 500f, "MaxHealth=500 时 HealthFromMaxHealth");

            text += Line($"MaxHealth 800→500，不读 MaxHealth，HealthFromMaxHealth：{healthBefore:0.00} → {healthAfter:0.00}");

            // 4.2 运行期反向边：Attack 的修饰器引用 Strength，BonusFromAttack 依赖 Attack。
            context.SetBase(LessonCatalog.Attack, 0f);
            context.SetBase(LessonCatalog.Strength, 10f);
            context.AddModifier(LessonCatalog.Attack, ModifierData.Add(
                LessonCatalog.Attack.ModifierKey,
                MagnitudeSource.Attribute(LessonCatalog.Strength.ModifierKey, 0.5f)));

            var attackAt10 = context.GetValue(LessonCatalog.Attack);
            var bonusAt10 = context.GetValue(LessonCatalog.BonusFromAttack);
            AssertNear(attackAt10, 5f, "Strength=10 时 Attack");
            AssertNear(bonusAt10, 10f, "Strength=10 时 BonusFromAttack");

            // 不读 Strength，也不读 Attack。
            context.SetBase(LessonCatalog.Strength, 20f);
            var bonusAt20 = context.GetValue(LessonCatalog.BonusFromAttack);
            var attackAt20 = context.GetValue(LessonCatalog.Attack);
            AssertNear(attackAt20, 10f, "Strength=20 时 Attack");
            AssertNear(bonusAt20, 20f, "Strength=20 时 BonusFromAttack");

            text += Line($"修饰器引用 Strength×0.5：力量 10→20，Attack {attackAt10:0.00} → {attackAt20:0.00}，BonusFromAttack {bonusAt10:0.00} → {bonusAt20:0.00}");

            // 4.3 上下文输入订阅
            var inputContext = LessonCatalog.NewContext();
            inputContext.AddModifier(LessonCatalog.LevelProbe, ModifierData.Add(
                LessonCatalog.LevelProbe.ModifierKey,
                MagnitudeSource.LevelCurve(10f, new[] { 1f, 1f, 3f, 2f }, 1f)));

            inputContext.Level = 1f;
            var level1 = inputContext.GetValue(LessonCatalog.LevelProbe);
            AssertNear(level1, 10f, "Level=1 时 LevelProbe");

            inputContext.Level = 3f;
            var level3 = inputContext.GetValue(LessonCatalog.LevelProbe);
            AssertNear(level3, 20f, "Level=3 时 LevelProbe");

            text += Line($"LevelCurve(10, {{1,1, 3,2}})：等级 1→3，LevelProbe {level1:0.00} → {level3:0.00}");

            inputContext.AddModifier(LessonCatalog.TimeProbe, ModifierData.Add(
                LessonCatalog.TimeProbe.ModifierKey,
                MagnitudeSource.TimeDecay(80f, 4f, DecayType.Linear)));

            inputContext.ElapsedTime = 0f;
            var time0 = inputContext.GetValue(LessonCatalog.TimeProbe);
            AssertNear(time0, 80f, "ElapsedTime=0 时 TimeProbe");

            inputContext.ElapsedTime = 2f;
            var time2 = inputContext.GetValue(LessonCatalog.TimeProbe);
            AssertNear(time2, 40f, "ElapsedTime=2 时 TimeProbe");

            text += Line($"线性 TimeDecay(80, 4)：已过时间 0→2，TimeProbe {time0:0.00} → {time2:0.00}");

            inputContext.AddModifier(LessonCatalog.Attack, ModifierData.Add(
                LessonCatalog.Attack.ModifierKey,
                MagnitudeSource.ContextFloat("buffPower", 1f, 0f)));
            inputContext.SetBase(LessonCatalog.Attack, 0f);

            var bonusBefore = inputContext.GetValue(LessonCatalog.BonusFromAttack);
            AssertNear(bonusBefore, 0f, "buffPower=0 时 BonusFromAttack");

            // 不读 Attack。
            inputContext.SetFloat("buffPower", 5f);
            var bonusAfter = inputContext.GetValue(LessonCatalog.BonusFromAttack);
            AssertNear(bonusAfter, 10f, "buffPower=5 时 BonusFromAttack");

            text += Line($"ContextFloat(\"buffPower\")：0→5，不读 Attack，BonusFromAttack {bonusBefore:0.00} → {bonusAfter:0.00}");
            text += Line("关键点: 写入时沿静态闭包、运行期反向边和上下文输入订阅标脏，只在读取时重算。");

            return text;
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (Math.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"[AK-INVALIDATE] {label} 期望 {expected:0.00}，实际 {actual:0.00}");
            }
        }
    }
}
