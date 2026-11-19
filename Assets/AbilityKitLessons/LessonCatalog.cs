using AbilityKit.Attributes;
using AbilityKit.Attributes.Core;
using AbilityKit.Attributes.Constraint;
using AbilityKit.Attributes.Formula;
using AbilityKit.Modifiers;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 所有课程共享的属性目录。
    /// 属性定义属于注册阶段；属性数值属于每个实体自己的 AttributeContext。
    /// </summary>
    public static class LessonCatalog
    {
        public static AttributeId Strength { get; private set; }
        public static AttributeId Attack { get; private set; }
        public static AttributeId MaxHealth { get; private set; }
        public static AttributeId Health { get; private set; }
        public static AttributeId MoveSpeed { get; private set; }
        public static AttributeId CritChance { get; private set; }
        public static AttributeId AttackFromStrength { get; private set; }
        public static AttributeId HealthFromMaxHealth { get; private set; }

        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;

            Strength = AttrRegistry.Register(new AttributeDef("Lesson.Strength", "Combat", 10f));
            Attack = AttrRegistry.Register(new AttributeDef("Lesson.Attack", "Combat", 100f));
            MaxHealth = AttrRegistry.Register(new AttributeDef("Lesson.MaxHealth", "Combat", 1000f));
            Health = AttrRegistry.Register(new AttributeDef("Lesson.Health", "Combat", 1000f, null, new RangeAttributeConstraint(0f, 10000f), MaxHealth));
            MoveSpeed = AttrRegistry.Register(new AttributeDef("Lesson.MoveSpeed", "Combat", 5f, null, new RangeAttributeConstraint(0f, 20f)));
            CritChance = AttrRegistry.Register(new AttributeDef("Lesson.CritChance", "Combat", 0.1f, null, new RangeAttributeConstraint(0f, 1f)));

            AttackFromStrength = AttrRegistry.Register(new AttributeDef(
                "Lesson.AttackFromStrength",
                "Combat",
                100f,
                new AttributeExpressionFormula("base + add + Lesson.Strength * 0.5"),
                null,
                Strength));

            HealthFromMaxHealth = AttrRegistry.Register(new AttributeDef(
                "Lesson.HealthFromMaxHealth",
                "Combat",
                0f,
                new AttributeExpressionFormula("min(base + add, Lesson.MaxHealth)"),
                new RangeAttributeConstraint(0f, 10000f),
                MaxHealth));

            _initialized = true;
        }

        public static AttributeContext NewContext(float level = 1f)
        {
            EnsureInitialized();
            return new AttributeContext { Level = level };
        }

        public static void Freeze()
        {
            EnsureInitialized();
            if (!((IAttributeRegistry)AttrRegistry.Instance).IsFrozen) AttrRegistry.Freeze();
        }

        public static ModifierKey StrengthKey
        {
            get
            {
                EnsureInitialized();
                //return ModifierKey.FromPacked(1u);
                return Strength.ModifierKey;
            }
        }

        public static string Format(float value) => value.ToString("0.00");
    }
}
