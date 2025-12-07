using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem; // 引用物品系统

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 搜刮模式：扫描周围物品 -> 移动 -> 拾取
    /// </summary>
    public class State_Scavenge : MaidStateBase
    {
        private const float SearchRadius = 8.0f; // 搜索范围（
        private const float PickupRadius = 3.0f;  // 实际执行拾取的距离
        
        private readonly List<Collider> _targets = new List<Collider>();
        private Collider _currentTarget;
        private float _scanTimer = 0f;
        private int _stuckCheck = 0; // 简单的防卡死计数

        public override void Enter()
        {
            // 1. 暂时接管大脑
            SetNativeBrainActive(false);
            
            // 2. 停止当前移动
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
                Controller.AI.aimTarget = null; // 停止瞄准，专心捡垃圾
            }

            Controller.MaidCharacter?.PopText("开始搜刮...");
            
            // 3. 立即执行一次扫描
            ScanForLoot();
        }

        public override void Update()
        {
            // 安全检查
            if (Controller.MainOwner == null) return;
            
            // A. 如果距离主人太远，强制结束搜刮（防丢）
            float distToOwner = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            if (distToOwner > Controller.HoldMaxDistance) // 复用驻守距离配置
            {
                Controller.MaidCharacter?.PopText("距离过远-归队");
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            // B. 敌人检测（如果有敌人，优先战斗）
            // 注意：这里需要你决定是否允许战斗打断搜刮。
            // 如果希望她“贪婪”一点，可以把这个判断去掉。
            if (Controller.AI != null && Controller.AI.searchedEnemy != null && !Controller.AI.searchedEnemy.health.IsDead)
            {
                Controller.MaidCharacter?.PopText("发现敌人！");
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            // C. 搜刮逻辑循环
            if (_currentTarget == null)
            {
                // 没有目标，尝试从列表中取一个
                if (_targets.Count > 0)
                {
                    _currentTarget = GetNearestTarget();
                }
                else
                {
                    // 列表空了，重新扫描或退出
                    _scanTimer += Time.deltaTime;
                    if (_scanTimer > 1.0f) 
                    {
                        ScanForLoot();
                        _scanTimer = 0f;
                        
                        // 扫描完还是没东西，那就结束状态
                        if (_targets.Count == 0)
                        {
                            Controller.MaidCharacter?.PopText("搜刮完毕");
                            Machine.ChangeState<State_Autonomous>();
                        }
                    }
                }
            }
            else
            {
                ProcessCurrentTarget();
            }
        }

        private void ProcessCurrentTarget()
        {
            if (_currentTarget == null || _currentTarget.gameObject == null)
            {
                _currentTarget = null;
                return;
            }

            float dist = Vector3.Distance(Controller.transform.position, _currentTarget.transform.position);

            if (dist <= PickupRadius)
            {
                // 到达距离，尝试拾取
                bool success = TryPickupSingle(_currentTarget);
                
                // 无论成功失败，都移除该目标，防止死循环
                _targets.Remove(_currentTarget);
                _currentTarget = null;
                
                if (success) Controller.AI.StopMove();
            }
            else
            {
                // 移动向目标
                if (Controller.AI != null)
                {
                    Controller.AI.MoveToPos(_currentTarget.transform.position);
                }
                
                // 简单的防卡死：如果一直走不到，可能卡住了
                // 实际项目中建议用 timer 判断位置是否变化
            }
        }

        // === 核心逻辑：基于你提供的反编译代码优化 ===
        private void ScanForLoot()
        {
            _targets.Clear();
            // 使用 OverlapSphere 而不是 NonAlloc，方便管理列表
            Collider[] hits = Physics.OverlapSphere(Controller.transform.position, SearchRadius);
            
            foreach (var hit in hits)
            {
                if (IsValidLoot(hit))
                {
                    _targets.Add(hit);
                }
            }
            
            CMDebug.Log($"扫描到 {_targets.Count} 个可搜刮物体");
        }
        
        private Collider GetNearestTarget()
        {
            Collider best = null;
            float minDst = float.MaxValue;
            Vector3 myPos = Controller.transform.position;

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var t = _targets[i];
                if (t == null) 
                {
                    _targets.RemoveAt(i);
                    continue;
                }

                float d = Vector3.Distance(myPos, t.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    best = t;
                }
            }
            return best;
        }

        private bool IsValidLoot(Collider col)
        {
            // 检查箱子
            var box = col.GetComponent<InteractableLootbox>();
            if (box != null && box.Inventory != null && box.Inventory.Content.Count > 0) return true;

            // 检查地上物品
            var pickup = col.GetComponent<InteractablePickup>();
            if (pickup != null && pickup.ItemAgent != null && pickup.ItemAgent.Item != null) return true;

            return false;
        }

        // 复用并净化了你的参考代码逻辑
        private bool TryPickupSingle(Collider col)
        {
            if (col == null) return false;
            
            var ai = Controller.AI;
            if (ai == null) return false;

            try
            {
                // 1. 处理箱子
                var box = col.GetComponent<InteractableLootbox>();
                if (box != null)
                {
                    // 简单的全部拿走逻辑
                    var items = new List<Item>(box.Inventory.Content);
                    bool pickedAny = false;
                    foreach (var item in items)
                    {
                        if (item.GetTotalRawValue() >= 0 && CanPick(ai))
                        {
                            if (ai.CharacterMainControl.PickupItem(item)) pickedAny = true;
                        }
                    }
                    return pickedAny;
                }

                // 2. 处理地上物品
                var pickup = col.GetComponent<InteractablePickup>();
                if (pickup != null && pickup.ItemAgent != null)
                {
                    var item = pickup.ItemAgent.Item;
                    if (item != null && item.GetTotalRawValue() >= 0 && CanPick(ai))
                    {
                        return ai.CharacterMainControl.PickupItem(item);
                    }
                }
            }
            catch 
            {
                // 忽略错误
            }
            return false;
        }

        private bool CanPick(AICharacterController ai)
        {
            // 检查背包是否有空位 (0 表示主背包)
            bool canPick = ai.CharacterMainControl.CharacterItem.Inventory.GetFirstEmptyPosition(0) >= 0;
            if (!canPick)
            {
                ai.CharacterMainControl.PopText("背包满了！");
            }
            
            return canPick;
        }
    }
}