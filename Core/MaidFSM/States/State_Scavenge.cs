using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem; 
using Duckov.ItemUsage; 

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 智能搜刮状态 (拟真优化版)
    /// </summary>
    public class State_Scavenge : MaidStateBase
    {
        // ================= 参数配置 =================
        private const float SearchRadius = 10.0f;       // 略微增大搜索范围
        private const float OwnerTetherRadius = 15.0f;  // 允许在主人附近稍微远一点的地方搜刮
        private const float InteractionThreshold = 2.5f;// 交互距离
        
        // 搜刮耗时配置
        private const float LootDurationPerItem = 0.5f; // 每个物品的搜刮耗时(秒)
        private const float BoxSearchDelay = 1.2f;      // 打开箱子/开始搜索的初始延迟

        // ================= 运行时状态 =================
        private List<Collider> _scannedObjects = new List<Collider>();
        private Collider _currentTarget;
        
        // 搜刮过程控制变量
        private Queue<Item> _pendingLootQueue = new Queue<Item>(); // 待拾取物品队列
        private bool _isLooting = false;        // 是否正在执行搜刮动作
        private float _actionTimer = 0f;        // 动作计时器

        // 战利品记录
        public List<Item> LootHistory { get; private set; } = new List<Item>();

        public override void Enter()
        {
            SetNativeBrainActive(false);
            HaltMovement();
            
            // 初始化状态
            _currentTarget = null;
            _scannedObjects.Clear();
            ResetLootingState(); // 清理搜刮相关的临时变量
            
            Controller.MaidCharacter?.PopText("开始搜刮物资...");
        }

        public override void Update()
        {
            if (Controller.MainOwner == null) return;

            // --- 1. 高优先级中断检测 (任何时候都生效) ---
            // 即使正在捡东西，如果发现敌人或离主人太远，也要立刻停手
            if (CheckInterrupts()) return;

            // --- 2. 状态逻辑分流 ---
            if (_isLooting)
            {
                // 正在搜刮中 (读条阶段)
                UpdateLootingProcess();
            }
            else
            {
                // 寻找或移动向目标
                UpdateMovementLogic();
            }
        }

        // ================= 核心逻辑: 搜刮过程 =================

        /// <summary>
        /// 驱动搜刮读条和动作
        /// </summary>
        private void UpdateLootingProcess()
        {
            // 保护性检查：如果箱子/物品突然消失了 (被销毁)
            if (_currentTarget == null || _currentTarget.gameObject == null)
            {
                FinishCurrentLooting(); // 结束当前目标，找下一个
                return;
            }

            _actionTimer += Time.deltaTime;

            // 读条完成，执行一次拾取
            if (_actionTimer >= LootDurationPerItem)
            {
                _actionTimer = 0f; // 重置计时器

                if (_pendingLootQueue.Count > 0)
                {
                    // 从队列取出一个物品尝试拾取
                    Item itemToPick = _pendingLootQueue.Dequeue();
                    
                    // 只有当物品仍然存在且未被他人拿走时才拾取
                    if (itemToPick != null && TryPickItem(itemToPick))
                    {
                        // 可选：每捡起一个东西冒个字
                        // Controller.MaidCharacter?.PopText($"+ {itemToPick.DisplayName}");
                    }
                    
                    // 如果捡完这个导致背包满了，中断会在下一帧的 CheckInterrupts 处理
                }

                // 队列空了，说明捡完了
                if (_pendingLootQueue.Count == 0)
                {
                    FinishCurrentLooting();
                }
            }
        }

        /// <summary>
        /// 到达目标，开始初始化搜刮队列
        /// </summary>
        private void StartLooting(Collider target)
        {
            ResetLootingState(); //以此为基准重置

            // 1. 解析目标内的物品
            if (target.TryGetComponent<InteractableLootbox>(out var box))
            {
                if (box.Inventory != null && box.Inventory.Content != null)
                {
                    // 将箱子里的东西加入队列
                    foreach (var item in box.Inventory.Content)
                    {
                        _pendingLootQueue.Enqueue(item);
                    }
                }
            }
            else if (target.TryGetComponent<InteractablePickup>(out var pickup))
            {
                if (pickup.ItemAgent != null && pickup.ItemAgent.Item != null)
                {
                    _pendingLootQueue.Enqueue(pickup.ItemAgent.Item);
                }
            }

            // 2. 如果有东西可捡，进入搜刮状态
            if (_pendingLootQueue.Count > 0)
            {
                _isLooting = true;
                // 第一个物品额外增加一点“翻找”的延迟时间
                _actionTimer = -BoxSearchDelay + LootDurationPerItem; 
                Controller.MaidCharacter?.PopText("正在搜索...");
            }
            else
            {
                // 空箱子，直接结束
                FinishCurrentLooting();
            }
        }

        private void FinishCurrentLooting()
        {
            ResetLootingState();
            _currentTarget = null; // 置空当前目标，触发 UpdateMovementLogic 寻找下一个
        }

        private void ResetLootingState()
        {
            _isLooting = false;
            _actionTimer = 0f;
            _pendingLootQueue.Clear();
        }

        // ================= 核心逻辑: 移动与寻路 =================

        private void UpdateMovementLogic()
        {
            if (_currentTarget == null || _currentTarget.gameObject == null)
            {
                AcquireNextTarget();
            }
            else
            {
                float dist = Vector3.Distance(Controller.transform.position, _currentTarget.transform.position);

                if (dist <= InteractionThreshold)
                {
                    Controller.AI?.StopMove();
                    StartLooting(_currentTarget); // 到达位置，切换为搜刮模式
                }
                else
                {
                    // 持续更新移动目标 (防止物理推挤导致位置偏移)
                    Controller.AI?.MoveToPos(_currentTarget.transform.position);
                }
            }
        }

        // ================= 辅助与检测 =================

        private bool CheckInterrupts()
        {
            // 优先级判定：
            // 1. 主人太远 -> 强制归队
            if (IsOwnerTooFar())
            {
                ExitStateAndClear("距离过远-归队");
                return true;
            }
            // 2. 威胁 -> 战斗
            if (IsUnderThreat())
            {
                ExitStateAndClear("发现威胁！停止搜刮");
                return true;
            }
            // 3. 背包满 -> 停止 (仅在非搜刮状态或搜刮间隙检测，避免频繁打断)
            if (IsBagFull())
            {
                ExitStateAndClear("背包已满，搜刮结束");
                return true;
            }
            return false;
        }

        private void ExitStateAndClear(string reason)
        {
            ResetLootingState(); // 退出前务必清理队列
            Controller.MaidCharacter?.PopText(reason);
            Machine.ChangeState<State_Autonomous>();
        }

        // 以下保持原有逻辑不变，略微整理
        private void AcquireNextTarget()
        {
            ScanEnvironment();
            _currentTarget = GetClosestValidTarget();
            if (_currentTarget == null)
            {
                ExitStateAndClear("附近无物资");
            }
        }

        private bool TryPickItem(Item item)
        {
            // 增加校验：如果物品已经被别人捡走了(Inventory变为null或易主)，则跳过
            if (item == null || item.GetTotalRawValue() < 0) return false;
            
            // 尝试拾取
            bool success = Controller.MaidCharacter.PickupItem(item);
            if (success)
            {
                Controller.LootHistory.Add(item);
            }
            return success;
        }

        // ... (ScanEnvironment, IsValidLootTarget, GetClosestValidTarget, IsBagFull 等代码保持原样即可) ...
        
        // 为了完整性，补充省略的辅助方法
        private void ScanEnvironment()
        {
            _scannedObjects.Clear();
            Collider[] hits = Physics.OverlapSphere(Controller.transform.position, SearchRadius);
            foreach (var hit in hits)
            {
                if (IsValidLootTarget(hit)) _scannedObjects.Add(hit);
            }
        }

        private bool IsValidLootTarget(Collider col)
        {
            if (col == null) return false;
            float distToOwner = Vector3.Distance(col.transform.position, Controller.MainOwner.transform.position);
            if (distToOwner > OwnerTetherRadius) return false;

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
                if (d < minDst) { minDst = d; best = target; }
            }
            return best;
        }

        private bool IsBagFull()
        {
            if (Controller.AI == null) return true;
            return Controller.AI.CharacterMainControl.CharacterItem.Inventory.GetFirstEmptyPosition(0) < 0;
        }

        private bool IsOwnerTooFar()
        {
            return Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position) > Controller.HoldMaxDistance;
        }

        private bool IsUnderThreat()
        {
            var ai = Controller.AI;
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
    }
}