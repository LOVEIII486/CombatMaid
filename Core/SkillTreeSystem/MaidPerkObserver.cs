using UnityEngine;
using Duckov.PerkTrees;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 挂载在每个 Perk 节点上，用于监听解锁状态变化
    /// </summary>
    public class MaidPerkObserver : MonoBehaviour
    {
        private Perk _perk;
        private bool _isUnlockedCached = false;
        private bool _initialized = false;

        private void Start()
        {
            _perk = GetComponent<Perk>();
            if (_perk != null)
            {
                // 初始化时同步状态，但不触发“新解锁”逻辑
                _isUnlockedCached = _perk.Unlocked;
                _initialized = true;
            }
        }

        private void Update()
        {
            if (!_initialized || _perk == null) return;

            // 检测状态是否发生了翻转（从 未解锁 -> 已解锁）
            if (_perk.Unlocked && !_isUnlockedCached)
            {
                _isUnlockedCached = true;
                OnUnlock();
            }
        }

        private void OnUnlock()
        {
            // 1. 通知管理器保存存档
            MaidSkillTreeManager.Instance.SaveProgress();
            
            // 2. 通知系统应用效果 (例如：如果正在战斗中，立即给女仆加buff)
            // 这里可以添加事件广播，或者直接调用 MaidManager刷新
            CMDebug.Log($"[MaidSkill] 节点已解锁: {_perk.name}");
            
            // 示例：如果在场景中有女仆，尝试刷新她的状态
            // MaidManager.Instance?.RefreshActiveMaidStats(); 
        }
    }
}