using System;
using AbilityKit.Attributes;
using AbilityKit.Attributes.Core;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 示例 12：注册顺序、注册表冻结和多 Context 隔离。
    /// </summary>
    public sealed class Lesson12_RegistryAndContexts : LessonExampleBase
    {
        public override string Title => "12 - 注册、冻结与多实体隔离";
        public override string Purpose => "理解 AttributeId 由注册顺序决定、冻结后不能再注册，以及同一套属性定义在不同 Context 上拥有独立数值。";

        public override string Run()
        {
            LessonCatalog.Freeze();

            var text = string.Empty;

            // 1. 注册顺序决定 RawId；下面的断言只依赖相对顺序，不写死绝对 ID。
            AssertBefore(LessonCatalog.Strength, LessonCatalog.Attack, "Strength 应早于 Attack 注册");
            AssertBefore(LessonCatalog.Attack, LessonCatalog.MaxHealth, "Attack 应早于 MaxHealth 注册");
            AssertBefore(LessonCatalog.MaxHealth, LessonCatalog.Health, "MaxHealth 应早于 Health 注册");
            AssertBefore(LessonCatalog.CritChance, LessonCatalog.AttackFromStrength, "CritChance 应早于 AttackFromStrength 注册");
            AssertBefore(LessonCatalog.TimeProbe, LessonCatalog.CustomFormulaProbe, "TimeProbe 应早于 CustomFormulaProbe 注册");

            text += Line($"注册顺序：Strength={LessonCatalog.Strength.RawId}，Attack={LessonCatalog.Attack.RawId}，MaxHealth={LessonCatalog.MaxHealth.RawId}，TimeProbe={LessonCatalog.TimeProbe.RawId}");
            text += Line("注意：在中间插入新属性会让后续 ID 整体后移，已有存档或网络协议不能依赖绝对 ID。");

            // 2. 冻结后 Register 和 Request 都会失败。
            var registry = (IAttributeRegistry)AttrRegistry.Instance;
            AssertTrue(registry.IsFrozen, "LessonCatalog.Freeze() 之后注册表应为冻结状态");

            var registerThrew = false;
            try
            {
                AttrRegistry.Register(new AttributeDef("Lesson.LateRegistration", "Combat", 0f));
            }
            catch (InvalidOperationException)
            {
                registerThrew = true;
            }

            var requestThrew = false;
            try
            {
                AttrRegistry.Attr("Lesson.UnknownAfterFreeze");
            }
            catch (InvalidOperationException)
            {
                requestThrew = true;
            }

            AssertTrue(registerThrew, "冻结后 Register 应抛出 InvalidOperationException");
            AssertTrue(requestThrew, "冻结后 Request 未注册名称应抛出 InvalidOperationException");
            text += Line($"冻结检查：Register 抛错={registerThrew}，Request 未注册名称抛错={requestThrew}");

            // 3. 同一套 AttributeDef，不同 Context 各自持有独立的数值和修改器。
            var left = LessonCatalog.NewContext();
            var right = LessonCatalog.NewContext();

            left.SetBase(LessonCatalog.Attack, 100f);
            right.SetBase(LessonCatalog.Attack, 200f);

            var leftBefore = left.GetValue(LessonCatalog.Attack);
            var rightBefore = right.GetValue(LessonCatalog.Attack);

            var buffHandle = left.AddModifier(LessonCatalog.Attack, ModifierData.Add(ModifierKey.AttackPower, 50f));

            var leftAfter = left.GetValue(LessonCatalog.Attack);
            var rightAfter = right.GetValue(LessonCatalog.Attack);

            AssertNear(leftBefore, 100f, "左侧初始 Attack");
            AssertNear(rightBefore, 200f, "右侧初始 Attack");
            AssertNear(leftAfter, 150f, "左侧加 Buff 后 Attack");
            AssertNear(rightAfter, 200f, "右侧不应受左侧 Buff 影响");

            text += Line($"多 Context：左 Attack {leftBefore:0.00} → {leftAfter:0.00}，右 Attack {rightBefore:0.00} → {rightAfter:0.00}");

            // 4. AttributeChanged 只在最终值真正变化时触发，且只属于对应 Context。
            var changedCount = 0;
            var changeOld = 0f;
            var changeNew = 0f;

            left.AttributeChanged += (group, id, oldValue, newValue) =>
            {
                if (id != LessonCatalog.Attack) return;
                changedCount++;
                changeOld = oldValue;
                changeNew = newValue;
            };

            // 先卸掉 Buff，让后面的 SetBase 直接体现基础值变化。
            left.RemoveModifier(LessonCatalog.Attack, buffHandle);
            left.SetBase(LessonCatalog.Attack, 120f);
            left.GetValue(LessonCatalog.Attack);

            AssertTrue(changedCount == 1, "Attack 变化应触发一次 AttributeChanged");
            AssertNear(changeOld, 150f, "AttributeChanged 旧值");
            AssertNear(changeNew, 120f, "AttributeChanged 新值");

            text += Line($"事件：左侧 Attack {changeOld:0.00} → {changeNew:0.00}，触发 {changedCount} 次；右侧没有订阅，不受影响。");
            text += Line("关键点: AttributeDef 是共享规格，AttributeContext 才是每个实体的独立运行时。");

            return text;
        }

        private static void AssertBefore(AttributeId first, AttributeId second, string label)
        {
            if (first.RawId <= 0 || second.RawId <= 0 || first.RawId >= second.RawId)
            {
                throw new InvalidOperationException(
                    $"[Lesson12] {label}：first={first.RawId}，second={second.RawId}");
            }
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"[Lesson12] {label}");
            }
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (Math.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"[Lesson12] {label} 期望 {expected:0.00}，实际 {actual:0.00}");
            }
        }
    }
}
