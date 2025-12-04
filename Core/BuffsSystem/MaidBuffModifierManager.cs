using System.Collections.Generic;
using ItemStatsSystem;
using ItemStatsSystem.Stats;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// 管理由 Buff 产生的属性修改器
    /// </summary>
    public class MaidBuffModifierManager
    {
        private static MaidBuffModifierManager _instance;
        public static MaidBuffModifierManager Instance => _instance ??= new MaidBuffModifierManager();

        // 存储结构: Buff实例ID -> [(属性, 修改器), ...]
        private Dictionary<int, List<(Stat stat, Modifier modifier)>> _buffModifiers 
            = new Dictionary<int, List<(Stat, Modifier)>>();

        /// <summary>
        /// 记录一个修改器，以便后续自动清理
        /// </summary>
        public void TrackModifier(int buffInstanceId, Stat stat, Modifier modifier)
        {
            if (stat == null || modifier == null) return;

            if (!_buffModifiers.ContainsKey(buffInstanceId))
            {
                _buffModifiers[buffInstanceId] = new List<(Stat, Modifier)>();
            }

            _buffModifiers[buffInstanceId].Add((stat, modifier));
        }

        /// <summary>
        /// 清理指定 Buff 的所有修改器
        /// </summary>
        public void CleanupModifiers(int buffInstanceId)
        {
            if (_buffModifiers.TryGetValue(buffInstanceId, out var modifiers))
            {
                foreach (var (stat, modifier) in modifiers)
                {
                    if (stat != null && modifier != null)
                    {
                        stat.RemoveModifier(modifier);
                    }
                }
                _buffModifiers.Remove(buffInstanceId);
            }
        }

        public void Clear()
        {
            _buffModifiers.Clear();
        }
    }
}