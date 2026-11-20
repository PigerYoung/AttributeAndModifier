using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// 属性上下文。
    /// 实现 IModifierContext 接口，可以与 AbilityKit.Modifiers 配合使用。
    /// 
    /// 重构说明：
    /// - 支持依赖注入 IAttributeRegistry
    /// - 移除对 AttributeRegistry.Instance 的硬依赖
    /// - 提供 IAttributeRegistry 属性和 SetRegistry 方法
    /// </summary>
    public sealed class AttributeContext : IModifierContext
    {
        private readonly Dictionary<string, AttributeGroup> _groups = new Dictionary<string, AttributeGroup>(StringComparer.Ordinal);
        private int _nextSourceId = 1;

        /// <summary>
        /// 每个 Context 一份运行期索引：修饰器反向边和上下文输入订阅。
        /// 静态闭包在注册表上，冻结后所有 Context 共享。
        /// </summary>
        private readonly AttributeInvalidationIndex _invalidation = new AttributeInvalidationIndex();

        private float _level = 1f;
        private float _elapsedTime;

        private int[] _visitMarks = new int[16];
        private int _visitStamp = 1;

        /// <summary>
        /// 注册表引用（可注入）
        /// </summary>
        private IAttributeRegistry _registry;

        /// <summary>
        /// 获取注册表
        /// </summary>
        public IAttributeRegistry Registry
        {
            get => _registry ?? AttributeRegistry.DefaultRegistry;
            set => _registry = value;
        }

        public IReadOnlyDictionary<string, AttributeGroup> Groups => _groups;

        /// <summary>
        /// 当前等级（用于 ScalableFloat 曲线插值）
        /// </summary>
        // [AK-INVALIDATE] 等级只失效订阅了 "level" 的属性，再带上它们的下游。
        // LevelProbe 使用 LevelCurve(10, {1,1, 3,2})：等级 1 读到 10，等级 3 读到 20。
        // TimeProbe 不在这张表里，不会变脏。
        public float Level
        {
            get => _level;
            set
            {
                if (Math.Abs(_level - value) < 0.00001f) return;
                _level = value;
                InvalidateInput("level");
            }
        }

        public event Action<string, AttributeId, float, float> AttributeChanged;

        #region 时间属性

        /// <summary>当前时间（秒）</summary>
        public float CurrentTime { get; set; }

        /// <summary>增量时间（秒）</summary>
        public float DeltaTime { get; set; }

        /// <summary>距离修改器生效的已过时间（秒）</summary>
        // [AK-INVALIDATE] 已过时间只失效订阅了 "elapsedTime" 的属性。
        // 线性 TimeDecay(80, 4)：ElapsedTime 0 读到 80，改为 2 后读到 40。
        // CurrentTime 不参与这条公式，不在这里失效。
        public float ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                if (Math.Abs(_elapsedTime - value) < 0.00001f) return;
                _elapsedTime = value;
                InvalidateInput("elapsedTime");
            }
        }

        #endregion

        #region 数据槽

        private readonly Dictionary<string, object> _dataSlots = new Dictionary<string, object>();

        /// <summary>
        /// 获取数据（泛型）
        /// </summary>
        public T GetData<T>(string key) where T : class
        {
            return _dataSlots.TryGetValue(key, out var obj) ? obj as T : null;
        }

        /// <summary>
        /// 尝试获取数据
        /// </summary>
        public bool TryGetData<T>(string key, out T value) where T : class
        {
            value = GetData<T>(key);
            return value != null;
        }

        /// <summary>
        /// 获取浮点数据
        /// </summary>
        public float GetFloat(string key)
        {
            if (_dataSlots.TryGetValue(key, out var obj))
            {
                return obj switch
                {
                    float f => f,
                    int i => i,
                    double d => (float)d,
                    _ => 0f
                };
            }
            return 0f;
        }

        /// <summary>
        /// 尝试获取浮点数据
        /// </summary>
        public bool TryGetFloat(string key, out float value)
        {
            value = GetFloat(key);
            return _dataSlots.ContainsKey(key);
        }

        /// <summary>
        /// 获取整型数据
        /// </summary>
        public int GetInt(string key)
        {
            if (_dataSlots.TryGetValue(key, out var obj))
            {
                return obj switch
                {
                    int i => i,
                    float f => (int)f,
                    double d => (int)d,
                    _ => 0
                };
            }
            return 0;
        }

        /// <summary>
        /// 尝试获取整型数据
        /// </summary>
        public bool TryGetInt(string key, out int value)
        {
            value = GetInt(key);
            return _dataSlots.ContainsKey(key);
        }

        /// <summary>
        /// 设置浮点数据
        /// </summary>
        // [AK-INVALIDATE] 按 key 失效持有者及其下游，不再扫描全部修饰器。
        // Attack 使用 ContextFloat("buffPower")，BonusFromAttack = Attack * 2。
        // SetFloat("buffPower", 5) 后不读 Attack，BonusFromAttack 下次读取为 10。
        // 新增日期这类输入时复用 InvalidateInput，不要新增 daySensitive 字段。
        public void SetFloat(string key, float value)
        {
            if (string.IsNullOrEmpty(key)) return;

            var changed = !_dataSlots.TryGetValue(key, out _) || Math.Abs(GetFloat(key) - value) > 0.00001f;
            _dataSlots[key] = value;
            if (changed)
            {
                InvalidateInput(key);
            }
        }

        /// <summary>
        /// 设置整型数据
        /// </summary>
        public void SetInt(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;

            var changed = !_dataSlots.TryGetValue(key, out _) || Math.Abs(GetFloat(key) - value) > 0.00001f;
            _dataSlots[key] = value;
            if (changed)
            {
                InvalidateInput(key);
            }
        }

        /// <summary>
        /// 设置数据
        /// </summary>
        public void SetData<T>(string key, T value) where T : class
        {
            _dataSlots[key] = value;
        }

        #endregion

        #region 元数据

        /// <summary>
        /// 修改器元数据
        /// </summary>
        public ModifierMetadata Metadata { get; set; }

        #endregion

        #region IModifierContext

        /// <summary>
        /// 获取属性值（实现 IModifierContext）
        /// </summary>
        float IModifierContext.GetAttribute(ModifierKey key)
        {
            var attrId = FindAttributeIdByKey(key);
            if (!attrId.IsValid) return 0f;
            return GetValue(attrId);
        }

        float IModifierContext.Level => Level;
        float IModifierContext.CurrentTime => CurrentTime;
        float IModifierContext.DeltaTime => DeltaTime;
        float IModifierContext.ElapsedTime => ElapsedTime;
        ModifierMetadata IModifierContext.Metadata => Metadata;

        #endregion

        #region 组管理

        public AttributeGroup GetOrCreateGroup(string group)
        {
            group ??= string.Empty;
            if (_groups.TryGetValue(group, out var g) && g != null) return g;
            return CreateGroup(group);
        }

        private AttributeGroup CreateGroup(string group)
        {
            var value = new AttributeGroup(group, this);
            value.AttributeChanged += (id, oldV, newV) =>
            {
                // [AK-INVALIDATE] 不在 AttributeChanged 里标脏。
                // Changed 只有读取并重算之后才会发生。
                // 若标脏留在这里，SetBase(Strength, 80) 后不读力量就读 DamageFromAttack，结果会停留在 210。
                AttributeChanged?.Invoke(group, id, oldV, newV);
            };
            _groups[group] = value;
            return value;
        }

        #region 失效

        /// <summary>
        /// [AK-INVALIDATE] 标记属性和它的全部下游为脏，不立即重算。
        /// </summary>
        public void Invalidate(AttributeId id)
        {
            if (!id.IsValid) return;

            int stamp = BeginVisit();
            Visit(id, stamp);
        }

        /// <summary>
        /// [AK-INVALIDATE] 标记订阅了指定上下文输入的属性和它们的下游为脏。
        /// 同一次遍历代号处理全部持有者，公共下游只走一次。
        /// </summary>
        public void InvalidateInput(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!_invalidation.TryGetInputSubscribers(key, out var holders)) return;

            int stamp = BeginVisit();
            foreach (var holder in holders)
            {
                Visit(AttributeId.FromRaw(holder.Key), stamp);
            }
        }

        // [AK-INVALIDATE] 只标脏，不重算。
        // 先标自己和静态闭包，再沿运行期边继续。
        // 闭包里的节点即使已经访问过，仍要走它的运行期边：
        // Strength 的闭包含 AttackFromStrength 时，Attack 上的 Attribute(Strength) 边不在闭包数组里。
        // 已访问的节点直接返回，截断 Strength ↔ Attack 这类运行期环。
        private void Visit(AttributeId id, int stamp)
        {
            if (!id.IsValid) return;

            int rawId = id.RawId;
            if (IsVisited(rawId, stamp)) return;
            MarkVisited(rawId, stamp);

            MarkExistingInstanceDirty(rawId);

            var downstream = Registry.GetDownstream(id);
            for (int i = 0; i < downstream.Count; i++)
            {
                var child = downstream[i];
                if (!child.IsValid) continue;

                int childRawId = child.RawId;
                if (IsVisited(childRawId, stamp)) continue;
                MarkVisited(childRawId, stamp);
                MarkExistingInstanceDirty(childRawId);
            }

            if (_invalidation.TryGetRuntimeHolders(rawId, out var holders))
            {
                foreach (var holder in holders)
                {
                    Visit(AttributeId.FromRaw(holder.Key), stamp);
                }
            }

            for (int i = 0; i < downstream.Count; i++)
            {
                var child = downstream[i];
                if (!child.IsValid) continue;

                int childRawId = child.RawId;
                if (_invalidation.TryGetRuntimeHolders(childRawId, out var childHolders))
                {
                    foreach (var holder in childHolders)
                    {
                        Visit(AttributeId.FromRaw(holder.Key), stamp);
                    }
                }
            }
        }

        /// <summary>
        /// 只标已有实例，不创建分组或实例。
        /// </summary>
        private void MarkExistingInstanceDirty(int rawId)
        {
            var id = AttributeId.FromRaw(rawId);
            var groupName = Registry.GetGroup(id) ?? string.Empty;
            if (!_groups.TryGetValue(groupName, out var group) || group == null) return;
            group.MarkDirty(id);
        }

        private int BeginVisit()
        {
            _visitStamp++;
            if (_visitStamp >= int.MaxValue)
            {
                Array.Clear(_visitMarks, 0, _visitMarks.Length);
                _visitStamp = 1;
            }

            return _visitStamp;
        }

        private bool IsVisited(int rawId, int stamp)
        {
            return rawId >= 0 && rawId < _visitMarks.Length && _visitMarks[rawId] == stamp;
        }

        private void MarkVisited(int rawId, int stamp)
        {
            if (rawId < 0) return;

            if (rawId >= _visitMarks.Length)
            {
                int newSize = _visitMarks.Length <= 0 ? 4 : _visitMarks.Length;
                while (newSize <= rawId)
                {
                    newSize *= 2;
                }

                var newMarks = new int[newSize];
                Array.Copy(_visitMarks, newMarks, _visitMarks.Length);
                _visitMarks = newMarks;
            }

            _visitMarks[rawId] = stamp;
        }

        internal void RegisterModifierDependencies(int holderRawId, in ModifierData modifier)
        {
            _invalidation.Register(holderRawId, modifier);
        }

        internal void UnregisterModifierDependencies(int holderRawId, in ModifierData modifier)
        {
            _invalidation.Unregister(holderRawId, modifier);
        }

        #endregion

        /// <summary>
        /// 获取属性所属的组
        /// </summary>
        public AttributeGroup GetGroupFor(AttributeId id)
        {
            var group = Registry.GetGroup(id) ?? string.Empty;
            return GetOrCreateGroup(group);
        }

        #endregion

        #region 属性值操作

        /// <summary>
        /// 获取属性值
        /// </summary>
        public float GetValue(AttributeId id)
        {
            return GetGroupFor(id).GetValue(id);
        }

        /// <summary>
        /// 设置基础值
        /// </summary>
        public void SetBase(AttributeId id, float baseValue)
        {
            GetGroupFor(id).SetBase(id, baseValue);
        }

        #endregion

        #region 修改器操作

        /// <summary>
        /// 添加修改器
        /// </summary>
        /// <param name="id">属性 ID</param>
        /// <param name="modifierData">修改器数据</param>
        /// <returns>修改器句柄</returns>
        public int AddModifier(AttributeId id, ModifierData modifierData)
        {
            return GetGroupFor(id).AddModifier(id, modifierData);
        }

        /// <summary>
        /// 添加修改器（便捷方法）
        /// </summary>
        public int AddModifier(AttributeId id, ModifierOp op, float value, int sourceId = 0)
        {
            return GetGroupFor(id).AddModifier(id, op, value, sourceId);
        }

        /// <summary>
        /// 移除修改器
        /// </summary>
        public bool RemoveModifier(AttributeId id, int handle)
        {
            return GetGroupFor(id).RemoveModifier(id, handle);
        }

        /// <summary>
        /// 清除指定来源的所有修改器
        /// </summary>
        /// <param name="sourceId">来源 ID</param>
        public void ClearModifiers(int sourceId)
        {
            foreach (var group in _groups.Values)
            {
                group.ClearModifiers(sourceId);
            }
        }

        #endregion

        #region 效果操作

        /// <summary>
        /// 应用属性效果
        /// </summary>
        /// <param name="effect">属性效果</param>
        /// <returns>效果对应的 SourceId，用于后续移除</returns>
        public int ApplyEffect(AttributeEffect effect)
        {
            if (effect == null || effect.Entries == null || effect.Entries.Length == 0) return 0;

            var sourceId = _nextSourceId++;

            for (int i = 0; i < effect.Entries.Length; i++)
            {
                var e = effect.Entries[i];
                if (!e.Attribute.IsValid) continue;

                var modifierData = e.ModifierData;
                modifierData.SourceId = sourceId;
                AddModifier(e.Attribute, modifierData);
            }

            return sourceId;
        }

        /// <summary>
        /// 移除指定效果
        /// </summary>
        /// <param name="sourceId">效果对应的 SourceId</param>
        public void RemoveEffect(int sourceId)
        {
            ClearModifiers(sourceId);
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 通过 ModifierKey 查找 AttributeId
        /// 默认实现：ModifierKey.Packed 与 AttributeId.Id 相同
        /// 可被子类覆盖此行为
        /// </summary>
        public AttributeId FindAttributeIdByKey(ModifierKey key)
        {
            if (key.IsEmpty) return default;
            return new AttributeId((int)key.Packed, null);
        }

        #endregion
    }
}
