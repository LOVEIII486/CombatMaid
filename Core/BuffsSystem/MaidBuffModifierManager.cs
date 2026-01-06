using System.Collections.Generic;
using ItemStatsSystem;
using ItemStatsSystem.Stats;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// Buff 修改器管理器
    /// </summary>
    public class MaidBuffModifierManager
    {
        private static MaidBuffModifierManager _instance;
        public static MaidBuffModifierManager Instance => _instance ??= new MaidBuffModifierManager();

        private readonly Dictionary<int, List<(Stat stat, Modifier modifier)>> _buffModifiers 
            = new Dictionary<int, List<(Stat, Modifier)>>();

        /// <summary>
        /// 记录一个修改器
        /// 内部自动处理 Stat 查找逻辑
        /// </summary>
        public void Track(int buffInstanceId, CharacterMainControl target, string statKey, Modifier modifier)
        {
            if (target == null || modifier == null) return;

            Stat targetStat = target.CharacterItem?.Stats.GetStat(statKey);
            if (targetStat == null)
            {
                targetStat = target.GetComponent<StatCollection>()?.GetStat(statKey);
            }

            if (targetStat != null)
            {
                TrackModifier(buffInstanceId, targetStat, modifier);
            }
            else
            {
                CMDebug.LogWarning($"[BuffManager] 追踪失败：角色 {target.name} 缺失属性 {statKey}");
            }
        }

        /// <summary>
        /// 核心记录方法：直接记录实例
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
                int count = 0;
                foreach (var (stat, modifier) in modifiers)
                {
                    // 增加健壮性检查：Stat 可能随角色销毁
                    if (stat != null && modifier != null)
                    {
                        stat.RemoveModifier(modifier);
                        count++;
                    }
                }
                _buffModifiers.Remove(buffInstanceId);
                
                if (count > 0)
                {
                    CMDebug.Log($"[BuffManager] 已清理 Buff({buffInstanceId}) 的 {count} 个属性修改器。");
                }
            }
        }

        public void Clear()
        {
            _buffModifiers.Clear();
            CMDebug.Log("[BuffManager] 全局清理完毕。");
        }
    }
}