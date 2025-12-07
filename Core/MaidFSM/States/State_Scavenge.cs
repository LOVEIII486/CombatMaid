using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem; // 物品系统
using Duckov.ItemUsage; // 物品交互

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 智能搜刮状态
    /// 特性：基于位置的动态优先级、主人距离牵引、战利品统计
    /// </summary>
    public class State_Scavenge : MaidStateBase
    {
        // ================= 参数配置 =================
        // 搜刮感知半径：AI 能看到多远的物品（以AI为圆心）
        private const float SearchRadius = 8.0f;       
        // 主人牵引半径：物品距离主人超过此距离则忽略（防止AI为了捡东西跑出屏幕）
        private const float OwnerTetherRadius = 12.0f; 
        // 交互距离：靠近到多少米开始拾取
        private const float InteractionThreshold = 3f; 

        // ================= 运行时状态 =================
        // 扫描缓存
        private List<Collider> _scannedObjects = new List<Collider>();
        // 当前锁定的物资
        private Collider _currentTarget;
        
        // ================= 核心数据 =================
        // [关键] 记录AI搜刮到的物品，供后续“吐出”功能使用
        public List<Item> LootHistory { get; private set; } = new List<Item>();

        public override void Enter()
        {
            SetNativeBrainActive(false);
            HaltMovement();
            
            _currentTarget = null;
            _scannedObjects.Clear();
            // 注意：不清空 LootHistory，这样可以在多次搜刮间累积战利品记录
            // 如果需要每次进状态清空，请取消下一行的注释
            // LootHistory.Clear();
            
            Controller.MaidCharacter?.PopText("开始搜刮物资...");
        }

        public override void Update()
        {
            if (Controller.MainOwner == null) return;

            // --- 阶段1: 状态中断检测 (优先级最高) ---
            
            // 1. 距离保护：离主人太远强制归队
            if (IsOwnerTooFar())
            {
                ExitState("距离过远-归队");
                return;
            }

            // 2. 威胁检测：发现敌人优先战斗
            if (IsUnderThreat())
            {
                ExitState("发现威胁！停止搜刮");
                return;
            }

            // 3. 满包检测：背包满了提示并退出
            if (IsBagFull())
            {
                ExitState("背包已满，搜刮结束");
                return;
            }

            // --- 阶段2: 搜刮行为执行 ---

            if (_currentTarget == null || _currentTarget.gameObject == null)
            {
                // 没有目标时，根据当前位置重新寻找最近的
                AcquireNextTarget();
            }
            else
            {
                // 有目标时，执行移动和交互
                MoveToAndInteract(_currentTarget);
            }
        }

        // ================= 核心逻辑方法 =================

        private void AcquireNextTarget()
        {
            // 核心修改：每次寻找都重新扫描环境
            // 确保永远选择离 AI 当前位置最近的物品，而不是沿用旧列表
            ScanEnvironment();

            _currentTarget = GetClosestValidTarget();

            if (_currentTarget == null)
            {
                // 扫描后依然没有有效目标，说明周围（在牵引范围内）已经空了
                ExitState("附近无物资");
            }
        }

        private void MoveToAndInteract(Collider target)
        {
            float dist = Vector3.Distance(Controller.transform.position, target.transform.position);

            if (dist <= InteractionThreshold)
            {
                // 到达交互距离，停止移动
                Controller.AI?.StopMove();
                
                // 执行拾取
                ProcessLoot(target);
                
                // 无论成功与否，处理完当前目标后置空，触发下一次重新扫描
                _currentTarget = null;
            }
            else
            {
                // 移动向目标
                Controller.AI?.MoveToPos(target.transform.position);
            }
        }

        private void ProcessLoot(Collider target)
        {
            if (target == null) return;

            // 情况A: 箱子 (InteractableLootbox)
            if (target.TryGetComponent<InteractableLootbox>(out var box))
            {
                if (box.Inventory != null && box.Inventory.Content != null)
                {
                    // 复制列表防止遍历时修改集合导致报错
                    var itemsInBox = new List<Item>(box.Inventory.Content);
                    foreach (var item in itemsInBox)
                    {
                        if (TryPickItem(item))
                        {
                            if (IsBagFull()) return; // 如果拿满，立即停止处理箱子
                        }
                    }
                }
            }
            // 情况B: 地面物品 (InteractablePickup)
            else if (target.TryGetComponent<InteractablePickup>(out var pickup))
            {
                if (pickup.ItemAgent != null && pickup.ItemAgent.Item != null)
                {
                    TryPickItem(pickup.ItemAgent.Item);
                }
            }
        }

        private bool TryPickItem(Item item)
        {
            if (item == null || item.GetTotalRawValue() < 0) return false;

            var maid = Controller.MaidCharacter;
            bool success = maid.PickupItem(item);

            if (success)
            {
                // 写入 Controller
                Controller.LootHistory.Add(item);
            }

            return success;
        }

        // ================= 扫描与筛选 =================

        private void ScanEnvironment()
        {
            _scannedObjects.Clear();
            
            // 以 AI 为圆心进行扫描
            Collider[] hits = Physics.OverlapSphere(Controller.transform.position, SearchRadius);
            
            foreach (var hit in hits)
            {
                if (IsValidLootTarget(hit))
                {
                    _scannedObjects.Add(hit);
                }
            }
        }

        private bool IsValidLootTarget(Collider col)
        {
            if (col == null) return false;

            // 规则1: 必须在主人附近的牵引范围内
            // 这是防止 AI 跑出屏幕的关键检查
            float distToOwner = Vector3.Distance(col.transform.position, Controller.MainOwner.transform.position);
            if (distToOwner > OwnerTetherRadius) return false;

            // 规则2: 必须是有效的箱子或物品
            bool isBox = col.TryGetComponent<InteractableLootbox>(out var box) && 
                         box.Inventory != null && box.Inventory.Content.Count > 0;
            
            bool isItem = col.TryGetComponent<InteractablePickup>(out var pickup) && 
                          pickup.ItemAgent != null && pickup.ItemAgent.Item != null;

            return isBox || isItem;
        }

        private Collider GetClosestValidTarget()
        {
            Collider best = null;
            float minDst = float.MaxValue;
            Vector3 myPos = Controller.transform.position;

            foreach (var target in _scannedObjects)
            {
                if (target == null) continue;

                float d = Vector3.Distance(myPos, target.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    best = target;
                }
            }
            return best;
        }

        // ================= 辅助检查方法 =================

        private bool IsBagFull()
        {
            if (Controller.AI == null) return true;
            // GetFirstEmptyPosition(0) 返回 -1 表示主背包没有空位
            return Controller.AI.CharacterMainControl.CharacterItem.Inventory.GetFirstEmptyPosition(0) < 0;
        }

        private bool IsOwnerTooFar()
        {
            float d = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            return d > Controller.HoldMaxDistance;
        }

        private bool IsUnderThreat()
        {
            var ai = Controller.AI;
            // 只有当敌人存在且活着时才算威胁
            return ai != null && ai.searchedEnemy != null && !ai.searchedEnemy.health.IsDead;
        }

        private void HaltMovement()
        {
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
                Controller.AI.aimTarget = null;
            }
        }

        private void ExitState(string reason)
        {
            Controller.MaidCharacter?.PopText(reason);
            Machine.ChangeState<State_Autonomous>();
        }
    }
}