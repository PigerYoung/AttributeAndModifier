using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// [AK-INVALIDATE] 一个属性上下文上的失效索引。
    /// 静态公式由注册表闭包处理。这里只记录战斗中才出现的两类关系：
    /// 修饰器引用了哪个属性，以及修饰器读了哪个上下文输入。
    ///
    /// 静态闭包例子：Strength 从 10 改为 80 后，不读 Strength。
    /// AttackFromStrength 的公式是 base + Strength * 0.5，DamageFromAttack = AttackFromStrength * 2。
    /// 闭包使三者都变脏，只读 DamageFromAttack 得到 280，而不是缓存里的 210。
    ///
    /// 运行期反向边例子：Attack 基础值 0，修饰器为 Strength * 0.5。
    /// 力量 10 时攻击力是 5；力量改为 20 后，不读力量，攻击力下次读取是 10。
    /// 依赖攻击力的 BonusFromAttack 同时变脏，下次读取是 20。
    ///
    /// 上下文输入例子：不要为每种来源写 xxxSensitive。
    /// "level" 上的 LevelCurve(10, {1,1, 3,2}) 在等级 1→3 时从 10 变为 20。
    /// "elapsedTime" 上的线性 TimeDecay(80, 4) 在时间 0→2 时从 80 变为 40。
    /// 以后的日期来源只追加键 "day"，由 Day 的写入调用 InvalidateInput("day")。
    /// </summary>
    internal sealed class AttributeInvalidationIndex
    {
        private readonly Dictionary<int, Dictionary<int, int>> _runtimeDependents =
            new Dictionary<int, Dictionary<int, int>>();

        private readonly Dictionary<string, Dictionary<int, int>> _inputSubscribers =
            new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);

        /// <summary>
        /// 登记一条修饰器带来的反向边和上下文订阅。
        /// </summary>
        public void Register(int holderRawId, in ModifierData modifier)
        {
            var notes = ModifierDependencyNotes.Collect(modifier);

            for (int i = 0; i < notes.SourceCount; i++)
            {
                Increment(_runtimeDependents, notes.GetSource(i), holderRawId);
            }

            for (int i = 0; i < notes.InputKeyCount; i++)
            {
                Increment(_inputSubscribers, notes.GetInputKey(i), holderRawId);
            }
        }

        /// <summary>
        /// 注销一条修饰器带来的反向边和上下文订阅。
        /// </summary>
        public void Unregister(int holderRawId, in ModifierData modifier)
        {
            var notes = ModifierDependencyNotes.Collect(modifier);

            for (int i = 0; i < notes.SourceCount; i++)
            {
                Decrement(_runtimeDependents, notes.GetSource(i), holderRawId);
            }

            for (int i = 0; i < notes.InputKeyCount; i++)
            {
                Decrement(_inputSubscribers, notes.GetInputKey(i), holderRawId);
            }
        }

        /// <summary>
        /// 获取引用了指定源属性的持有者及引用计数。源 → 持有者。
        /// </summary>
        public bool TryGetRuntimeHolders(int sourceRawId, out Dictionary<int, int> holders)
        {
            return _runtimeDependents.TryGetValue(sourceRawId, out holders);
        }

        /// <summary>
        /// 获取订阅了指定上下文输入的持有者及引用计数。
        /// </summary>
        public bool TryGetInputSubscribers(string key, out Dictionary<int, int> holders)
        {
            if (string.IsNullOrEmpty(key))
            {
                holders = null;
                return false;
            }

            return _inputSubscribers.TryGetValue(key, out holders);
        }

        private static void Increment<TKey>(Dictionary<TKey, Dictionary<int, int>> table, TKey key, int holderRawId)
        {
            if (!table.TryGetValue(key, out var holders))
            {
                holders = new Dictionary<int, int>(4);
                table[key] = holders;
            }

            holders.TryGetValue(holderRawId, out int count);
            holders[holderRawId] = count + 1;
        }

        private static void Decrement<TKey>(Dictionary<TKey, Dictionary<int, int>> table, TKey key, int holderRawId)
        {
            if (!table.TryGetValue(key, out var holders)) return;
            if (!holders.TryGetValue(holderRawId, out int count)) return;

            if (count <= 1)
            {
                holders.Remove(holderRawId);
                if (holders.Count == 0)
                {
                    table.Remove(key);
                }
                return;
            }

            holders[holderRawId] = count - 1;
        }
    }
}
