using System;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 14：实时值与快照值。
    /// </summary>
    public sealed class Lesson14_SnapshotVsLive : LessonExampleBase
    {
        public override string Title => "14 - 实时值与快照值";
        public override string Purpose => "理解 MagnitudeSource.Attribute 的实时引用，以及技能/伤害对象在某个时点捕获数值后与源属性解耦的快照语义。";

        public override string Run()
        {
            var context = LessonCatalog.NewContext();

            // 实时：Attack 的修改器保存“读取当前 Strength × 0.5”的规则。
            context.SetBase(LessonCatalog.Attack, 0f);
            context.SetBase(LessonCatalog.Strength, 10f);
            context.AddModifier(
                LessonCatalog.Attack,
                ModifierData.Add(
                    LessonCatalog.Attack.ModifierKey,
                    MagnitudeSource.Attribute(LessonCatalog.Strength.ModifierKey, 0.5f)));

            var liveAttackAt10 = context.GetValue(LessonCatalog.Attack);
            var liveBonusAt10 = context.GetValue(LessonCatalog.BonusFromAttack);
            AssertNear(liveAttackAt10, 5f, "Strength=10 时 Attack");
            AssertNear(liveBonusAt10, 10f, "Strength=10 时 BonusFromAttack");

            // 快照：模拟技能命中时把当时的数值固化到伤害对象里。
            var packet = new DamagePacket(liveAttackAt10 * 2f);

            // 源属性变化后，实时值继续跟随，快照值不受影响。
            context.SetBase(LessonCatalog.Strength, 20f);

            var liveAttackAt20 = context.GetValue(LessonCatalog.Attack);
            var liveBonusAt20 = context.GetValue(LessonCatalog.BonusFromAttack);
            var snapshotAfter = packet.Amount;

            AssertNear(liveAttackAt20, 10f, "Strength=20 时 Attack");
            AssertNear(liveBonusAt20, 20f, "Strength=20 时 BonusFromAttack");
            AssertNear(snapshotAfter, 10f, "已生成伤害包的快照值");

            return Line($"实时引用：Strength 10→20，Attack {liveAttackAt10:0.00} → {liveAttackAt20:0.00}，BonusFromAttack {liveBonusAt10:0.00} → {liveBonusAt20:0.00}") +
                   Line($"快照：命中时记录 Attack={liveAttackAt10:0.00}，伤害包={snapshotAfter:0.00}；Strength 改为 20 后仍然是 {snapshotAfter:0.00}") +
                   Line("关键点: 实时值每次读取都重新计算；快照值在时点捕获后与源属性解耦。");
        }

        /// <summary>
        /// 玩法层的伤害对象：在创建时记录数值，之后不再读取属性。
        /// </summary>
        private readonly struct DamagePacket
        {
            public readonly float Amount;

            public DamagePacket(float amount)
            {
                Amount = amount;
            }
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (Math.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"[Lesson14] {label} 期望 {expected:0.00}，实际 {actual:0.00}");
            }
        }
    }
}
