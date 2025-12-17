using System.Collections.Generic;
using System.Linq;
using CombatMaid.Settings;
using UnityEngine;
using ItemStatsSystem;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 智能搜刮状态 (拟真优化版 - 增强寻路与交互)
    /// </summary>
    public class State_Scavenge : MaidStateBase
    {
        // ================= 参数配置 =================
        private const float SearchRadius = 12.0f;       // 搜索范围
        private const float OwnerTetherRadius = 18.0f;  // 主人牵引范围
        private const float InteractionThreshold = 1.1f;  // 交互距离
        private const float VerticalInteractionRange = 2.5f;
        
        // 搜刮耗时配置
        private const float LootDurationPerItem = 0.5f; 
        private const float BoxSearchDelay = 1.0f;

        // 防卡死与寻路配置
        private const float StuckTimeout = 2.0f;         // 卡死判定时间
        private const float MinMoveDistance = 0.1f;      // 判定移动的最小距离
        private const float StuckCheckInterval = 0.5f;   // 卡死检测频率
        private const float DoorCheckInterval = 0.5f;    // 开门检测频率
        private const float DoorCheckRadius = 1.8f;      // 门检测半径

        // ================= 运行时状态 =================
        private List<Collider> _scannedObjects = new List<Collider>();
        private Collider _currentTarget;
        
        // 搜刮过程控制变量
        private Queue<Item> _pendingLootQueue = new Queue<Item>(); 
        private bool _isLooting = false;        
        private float _actionTimer = 0f;        

        // 卡死检测与黑名单变量
        private HashSet<Collider> _failedTargets = new HashSet<Collider>(); // 临时黑名单
        private Vector3 _lastPosForStuckCheck;
        private float _stuckTimer = 0f;
        private float _stuckCheckTimer = 0f;
        private float _doorCheckTimer = 0f;

        // 战利品记录 (本地引用，实际数据写入Controller)
        public List<Item> LootHistory { get; private set; } = new List<Item>();

        public override void Enter()
        {
            SetNativeBrainActive(false);
            HaltMovement();
            
            // 初始化状态
            _currentTarget = null;
            _scannedObjects.Clear();
            _failedTargets.Clear(); // 每次重新进入状态时清空黑名单，给之前的失败目标一次重试机会
            
            ResetLootingState(); 
            ResetStuckCheck(); // 重置卡死检测
            
            Controller.MaidCharacter?.PopText("开始搜刮物资...");
        }

        public override void Update()
        {
            if (Controller.MainOwner == null) return;

            // --- 1. 高优先级中断检测 ---
            if (CheckInterrupts()) return;

            // --- 2. 状态逻辑分流 ---
            if (_isLooting)
            {
                UpdateLootingProcess();
            }
            else
            {
                // [修复] 移动向目标时，同时进行防卡死检测和自动开门
                // 必须按顺序判断，如果前一步导致了状态变化或目标丢失，直接 return
        
                UpdateMovementLogic();
                // 如果在移动逻辑中切换了目标或状态，停止本帧
                if (_currentTarget == null) return; 

                bool stuckHandled = UpdateStuckCheck(); // 修改返回值
                if (stuckHandled) return; // [关键] 如果处理了卡死（可能导致状态切换），立刻停止！

                UpdateDoorCheck();
            }
        }

        // ================= 核心逻辑: 搜刮过程 =================

        private void UpdateLootingProcess()
        {
            // 保护性检查：如果箱子/物品突然消失了
            if (_currentTarget == null || _currentTarget.gameObject == null)
            {
                FinishCurrentLooting(); 
                return;
            }

            _actionTimer += Time.deltaTime;

            if (_actionTimer >= LootDurationPerItem)
            {
                _actionTimer = 0f; 

                if (_pendingLootQueue.Count > 0)
                {
                    // [修复漏洞] 在从队列取出物品(Dequeue)之前，必须先检查背包是否已满！
                    if (IsBagFull())
                    {
                        // 如果背包满了，立刻停止当前搜刮
                        // 剩下的物品会留在 _pendingLootQueue 里被清除，但实际上它们还在箱子里没有被动过
                        FinishCurrentLooting(); 
                
                        // 这里不需要额外调用 ExitState，因为 FinishCurrentLooting 会把 _isLooting 设为 false
                        // 下一帧 Update 的 CheckInterrupts 就会检测到背包满并自动退出状态
                        return;
                    }

                    Item itemToPick = _pendingLootQueue.Dequeue();
            
                    // 鲁棒性检查：确保物品未被销毁且有效
                    if (itemToPick != null && TryPickItem(itemToPick))
                    {
                        // 成功拾取
                    }
                }

                if (_pendingLootQueue.Count == 0)
                {
                    FinishCurrentLooting();
                }
            }
        }

        private void StartLooting(Collider target)
        {
            ResetLootingState();

            int minVal = CombatMaidConfig.LootMinVal; 

            // 1. 解析目标内的物品
            if (target.TryGetComponent<InteractableLootbox>(out var box))
            {
                if (box.Inventory != null && box.Inventory.Content != null)
                {
                    foreach (var item in box.Inventory.Content)
                    {
                        if (item != null && item.GetTotalRawValue() >= minVal)
                        {
                            _pendingLootQueue.Enqueue(item);
                        }
                    }
                }
            }
            else if (target.TryGetComponent<InteractablePickup>(out var pickup))
            {
                if (pickup.ItemAgent != null && pickup.ItemAgent.Item != null)
                {
                    if (pickup.ItemAgent.Item.GetTotalRawValue() >= minVal)
                    {
                        _pendingLootQueue.Enqueue(pickup.ItemAgent.Item);
                    }
                }
            }

            // 2. 如果有东西可捡，进入搜刮状态
            if (_pendingLootQueue.Count > 0)
            {
                _isLooting = true;
                _actionTimer = -BoxSearchDelay + LootDurationPerItem; 
                Controller.MaidCharacter?.PopText("正在搜刮...");
                HaltMovement(); // 确保停下来
            }
            else
            {
                // 目标是空的，结束并标记
                Controller.MaidCharacter?.PopText("没有什么值得搜刮的...");
                FinishCurrentLooting();
            }
        }

        private void FinishCurrentLooting()
        {
            // 标记箱子为已搜索
            if (_currentTarget != null && _currentTarget.gameObject != null)
            {
                if (_currentTarget.TryGetComponent<InteractableLootbox>(out var box))
                {
                    if (box.Inventory != null) box.Inventory.hasBeenInspectedInLootBox = true;
                    box.SetMarkerUsed();
                }
            }

            ResetLootingState();
            _currentTarget = null; 
            // 搜刮完成后，重置卡死检测，准备前往下一个目标
            ResetStuckCheck();
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
            // 如果没有目标或目标已销毁，寻找下一个
            if (_currentTarget == null || _currentTarget.gameObject == null)
            {
                AcquireNextTarget();
                return;
            }

            Vector3 myPos = Controller.transform.position;
            Vector3 targetPos = _currentTarget.transform.position;

            // 1. 水平距离
            float horizontalDist = Vector2.Distance(
                new Vector2(myPos.x, myPos.z), 
                new Vector2(targetPos.x, targetPos.z)
            );
            // 2. 垂直高度差
            float heightDiff = Mathf.Abs(myPos.y - targetPos.y);

            // 3. 距离判定
            bool isReach = (horizontalDist <= InteractionThreshold && heightDiff <= VerticalInteractionRange) 
                           || Vector3.Distance(myPos, targetPos) <= InteractionThreshold;

            if (isReach)
            {
                Controller.AI?.StopMove();
                StartLooting(_currentTarget);
            }
            else
            {
                // 持续请求移动（防止之前的 StopMove 导致停滞）
                // 注意：MoveToPos 内部有优化，重复调用消耗不大
                Controller.AI?.MoveToPos(targetPos);
            }
        }

        // ================= 增强逻辑: 防卡死与开门 =================

        private void ResetStuckCheck()
        {
            _stuckTimer = 0f;
            _stuckCheckTimer = 0f;
            _lastPosForStuckCheck = Controller.transform.position;
            _doorCheckTimer = 0f;
        }

        private bool UpdateStuckCheck()
        {
            // 如果没有目标，不需要检测卡死
            if (_currentTarget == null) return false;

            _stuckCheckTimer += Time.deltaTime;
            if (_stuckCheckTimer < StuckCheckInterval) return false;
    
            _stuckCheckTimer = 0f;

            bool isTryingToMove = Controller.AI != null && Controller.AI.IsMoving();
            float distMoved = Vector3.Distance(Controller.transform.position, _lastPosForStuckCheck);
            _lastPosForStuckCheck = Controller.transform.position;

            if (isTryingToMove && distMoved < MinMoveDistance)
            {
                _stuckTimer += StuckCheckInterval;
                if (_stuckTimer >= StuckTimeout)
                {
                    HandleStuck();
                    return true; // [关键] 报告已处理卡死
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
            return false;
        }

        private void HandleStuck()
        {
            // [修复] 使用 Unity 的生命周期检查 (implicit bool check)
            // 只有当物体真的还存在时，才获取它的 name 和加入黑名单
            if (_currentTarget) 
            {
                CMDebug.LogWarning($"搜刮过程卡死！放弃目标: {_currentTarget.name}");
                _failedTargets.Add(_currentTarget);
            }

            Controller.MaidCharacter?.PopText("过不去...");

            // 2. 停止移动并重置状态
            HaltMovement();
            _currentTarget = null;
            ResetStuckCheck();

            // 3. 立即尝试获取下一个目标
            AcquireNextTarget();
        }

        private void UpdateDoorCheck()
        {
            // 只有在试图移动时才检测开门
            if (Controller.AI == null || !Controller.AI.IsMoving()) return;

            _doorCheckTimer += Time.deltaTime;
            if (_doorCheckTimer < DoorCheckInterval) return;
            _doorCheckTimer = 0f;

            TryOpenNearbyDoors();
        }

        private void TryOpenNearbyDoors()
        {
            Collider[] hits = Physics.OverlapSphere(Controller.transform.position, DoorCheckRadius);
            foreach (var hit in hits)
            {
                Door door = hit.GetComponentInParent<Door>();
                if (door == null) continue;

                // 自动开门条件：关着 + 无锁(不需要物品) + 可交互
                if (!door.IsOpen && 
                    door.Interact != null && 
                    !door.Interact.requireItem && 
                    door.Interact.CheckInteractable())
                {
                    CMDebug.Log($"尝试自动开门: {door.name}");
                    Controller.MaidCharacter.Interact(door.Interact);
                    return;
                }
            }
        }

        // ================= 辅助与检测 =================

        private bool CheckInterrupts()
        {
            if (IsOwnerTooFar())
            {
                ExitStateAndClear("距离过远-归队");
                return true;
            }
            if (IsUnderThreat())
            {
                ExitStateAndClear("发现威胁！停止搜刮");
                return true;
            }
            // 只有在非 Looting 状态下才检测背包满，防止搜刮到一半被打断
            // 或者在 UpdateLootingProcess 内部处理了
            if (!_isLooting && IsBagFull())
            {
                ExitStateAndClear("背包已满，搜刮结束");
                return true;
            }
            return false;
        }

        private void ExitStateAndClear(string reason)
        {
            ResetLootingState(); 
            Controller.MaidCharacter?.PopText(reason);
            Machine.ChangeState<State_Autonomous>();
        }

        private void AcquireNextTarget()
        {
            ScanEnvironment();
            _currentTarget = GetClosestValidTarget();
            
            // 重置卡死检测，给新目标一个完整的 3s 机会
            ResetStuckCheck();

            if (_currentTarget == null)
            {
                // 如果扫描了一圈发现没有有效目标（或者全是黑名单里的），退出状态
                ExitStateAndClear("附近无物资");
            }
        }

        private bool TryPickItem(Item item)
        {
            if (Controller == null || Controller.MaidCharacter == null) return false;
            if (item == null || item.GetTotalRawValue() < 0) return false;
    
            bool success = Controller.MaidCharacter.PickupItem(item);
            if (success)
            {
                if (Controller.LootHistory != null)
                {
                    Controller.LootHistory.Add(item);
                }
            }
            return success;
        }
        
        private void ScanEnvironment()
        {
            _scannedObjects.Clear();
            Collider[] hits = Physics.OverlapSphere(Controller.transform.position, SearchRadius);
            foreach (var hit in hits)
            {
                // 过滤掉之前失败的目标
                if (_failedTargets.Contains(hit)) continue;

                if (IsValidLootTarget(hit)) _scannedObjects.Add(hit);
            }
        }

        private bool IsValidLootTarget(Collider col)
        {
            if (col == null) return false;
            float distToOwner = Vector3.Distance(col.transform.position, Controller.MainOwner.transform.position);
            if (distToOwner > OwnerTetherRadius) return false;
        
            if (CombatMaidConfig.IgnoreSearched)
            {
                if (col.TryGetComponent<InteractableLootbox>(out var checkBox))
                {
                    if (checkBox.Inventory != null && checkBox.Inventory.hasBeenInspectedInLootBox)
                    {
                        return false; 
                    }
                }
            }
            
            int minVal = CombatMaidConfig.LootMinVal;
            bool isValidBox = false;
            if (col.TryGetComponent<InteractableLootbox>(out var box) && 
                box.Inventory != null && box.Inventory.Content.Count > 0)
            {
                isValidBox = box.Inventory.Content.Any(item => item != null && item.GetTotalRawValue() >= minVal);
            }

            bool isValidItem = false;
            if (col.TryGetComponent<InteractablePickup>(out var pickup) && 
                pickup.ItemAgent != null && pickup.ItemAgent.Item != null)
            {
                isValidItem = pickup.ItemAgent.Item.GetTotalRawValue() >= minVal;
            }

            return isValidBox || isValidItem;
        }

        private Collider GetClosestValidTarget()
        {
            Collider best = null;
            float minDst = float.MaxValue;
            Vector3 myPos = Controller.transform.position;
            foreach (var target in _scannedObjects)
            {
                if (target == null) continue;
                // 再次过滤黑名单（双重保险）
                if (_failedTargets.Contains(target)) continue;

                float d = Vector3.Distance(myPos, target.transform.position);
                if (d < minDst) { minDst = d; best = target; }
            }
            return best;
        }

        private bool IsBagFull()
        {
            var character = Controller?.AI?.CharacterMainControl;
            if (character == null) return true;

            var item = character.CharacterItem;
            if (item == null) return true;

            var inventory = item.Inventory;
            if (inventory == null) return true;

            return inventory.GetFirstEmptyPosition(0) < 0;
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