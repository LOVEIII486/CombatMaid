using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidBehaviors;

namespace CombatMaid.Core
{
    [RequireComponent(typeof(MaidMovement))]
    public class MaidController : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidController]";
        
        private static readonly Dictionary<AICharacterController, MaidController> _maidRegistry 
            = new Dictionary<AICharacterController, MaidController>();

        public static MaidController GetMaid(AICharacterController ai)
        {
            if (ai == null) return null;
            return _maidRegistry.TryGetValue(ai, out var maid) ? maid : null;
        }
        
        // ==================== 核心引用 ====================
        
        public AICharacterController AI { get; private set; }
        public CharacterMainControl MaidCharacter => AI != null ? AI.CharacterMainControl : null;
        public CharacterMainControl MainOwner { get; private set; }
        public MaidMovement Movement { get; private set; }
        
        // [新增] 补血模块引用
        public MaidHeal HealBehavior { get; private set; }

        // ==================== 防卡死设置 ====================
        
        [Header("Anti-Stuck Settings")]
        public float ForceFollowDistance = 15.0f;
        public float TeleportDistance = 30.0f; 
        public float TeleportTimeout = 8.0f;
        public float SafeDistanceToResumeCombat = 10.0f;
        
        private float _forceFollowTimer = 0f;
        private bool _isForceFollowing = false;

        // ==================== 状态控制 ====================
        
        public bool IsPeaceMode { get; private set; } = false;
        
        public bool IsOverrideActive => Movement != null && Movement.IsActive;
        public float LastManualMoveTime { get; set; } = -999f;

        // 注意：这里移除了 MaidProfile 参数，因为你提供的文件中 Initialize 签名是 (MaidProfile, CharacterMainControl)
        // 但在上一轮上传的文件中你的 MaidProfile 是 Core.MaidProfile，请确保命名空间正确
        public void Initialize(MaidProfile profile, CharacterMainControl player)
        {
            MainOwner = player;

            AI = GetComponent<AICharacterController>();
            if (AI == null) AI = GetComponentInChildren<AICharacterController>();
            
            if (AI == null)
            {
                Debug.LogError($"{LogTag} 严重错误：找不到 AICharacterController！");
                return;
            }

            if (!_maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Add(AI, this);
            }

            AI.leader = player;
            AI.patrolRange = 100.0f;
            AI.patrolPosition = player.transform.position;

            // 初始化移动模块
            Movement = GetComponent<MaidMovement>();
            if (Movement == null) Movement = gameObject.AddComponent<MaidMovement>();
            Movement.Initialize(this); 

            // 初始化自动补血模块
            HealBehavior = GetComponent<MaidHeal>();
            if (HealBehavior == null) HealBehavior = gameObject.AddComponent<MaidHeal>();
            HealBehavior.Initialize(AI);

            Debug.Log($"{LogTag} {profile.Config.CustomName} 初始化完毕 (含自动补血)");
            
            //MaidBrainInjector.Inject(AI);
        }

        private void OnDestroy()
        {
            if (AI != null && _maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Remove(AI);
            }
        }

        /// <summary>
        /// 和平模式 - 只清除目标，保留警戒状态
        /// </summary>
        public void SetPeaceMode(bool enable)
        {
            IsPeaceMode = enable;
            
            if (enable && AI != null)
            {
                AI.searchedEnemy = null;
                AI.aimTarget = null;
                
                Debug.Log($"{LogTag} 和平模式：清除战斗目标，保持警戒状态");
            }
        }

        private void Update()
        {
            // 驱动移动模块
            if (Movement != null) Movement.OnUpdate();
            
            // [新增] 驱动补血模块
            if (HealBehavior != null) HealBehavior.OnUpdate();

            UpdateSmartFollow();
            
            // 持续更新巡逻位置到主人位置
            if (AI != null && MainOwner != null)
            {
                // [修改] 如果最近执行过手动移动（比如 10秒内），或者是强制跟随模式，才允许更新巡逻点。
                // 否则，保持她在原地（AI.patrolPosition 保持不变）
                
                bool justMoved = Time.time - LastManualMoveTime < 20.0f; // 20秒内算“驻守”
                bool tooFar = Vector3.Distance(AI.transform.position, MainOwner.transform.position) > ForceFollowDistance;

                // 逻辑：如果没在驻守，或者距离太远触发了强制跟随，就更新巡逻点为玩家
                if (!justMoved || tooFar)
                {
                    AI.patrolPosition = MainOwner.transform.position;
                }
                // 否则：AI.patrolPosition 会停留在她移动到的位置，她会在那里巡逻/警戒
            }
        }

        public void ForceMoveTo(Vector3 position)
        {
            if (Movement != null) Movement.MoveTo(position);
        }

        // ==================== 智能跟随系统 ====================

        private void UpdateSmartFollow()
        {
            if (MainOwner == null || AI == null || MaidCharacter == null || MaidCharacter.Health.IsDead) 
                return;

            if (Movement != null && Movement.IsActive)
                return;

            float dist = Vector3.Distance(MaidCharacter.transform.position, MainOwner.transform.position);

            if (dist > TeleportDistance)
            {
                TeleportToOwner();
                return;
            }

            if (dist > ForceFollowDistance)
            {
                if (!_isForceFollowing)
                {
                    EnterForceFollowMode();
                }
                
                _forceFollowTimer += Time.deltaTime;
                
                if (_forceFollowTimer > TeleportTimeout)
                {
                    TeleportToOwner();
                    _forceFollowTimer = 0f;
                }
                else
                {
                    // 检查 AI 是否在移动向主人
                    if (!IsAIMoving())
                    {
                        SendMoveCommandToOwner();
                    }
                }
            }
            else if (dist <= SafeDistanceToResumeCombat)
            {
                _forceFollowTimer = 0f;
                
                if (_isForceFollowing)
                {
                    ExitForceFollowMode();
                }
            }
            else
            {
                _forceFollowTimer = 0f;
            }
        }

        private bool IsAIMoving()
        {
            if (AI == null) return false;
            if (AI.WaitingForPathResult()) return true;
            if (AI.IsMoving()) return true;
            if (AI.HasPath() && !AI.ReachedEndOfPath()) return true;
            return false;
        }
        
        private void SendMoveCommandToOwner()
        {
            if (AI == null || MainOwner == null) return;
            
            Vector3 targetPos = MainOwner.transform.position;
            
            AI.patrolPosition = targetPos;
            
            if (AI.searchedEnemy != null)
            {
                AI.StopMove();
                AI.MoveToPos(targetPos);
            }
            
            Debug.Log($"{LogTag} {MaidCharacter?.name} 更新跟随目标 (距离: {Vector3.Distance(MaidCharacter.transform.position, targetPos):F1}m)");
        }

        private void EnterForceFollowMode()
        {
            _isForceFollowing = true;
            _forceFollowTimer = 0f;
            
            if (AI != null)
            {
                bool hasEnemy = AI.searchedEnemy != null;
                
                if (hasEnemy)
                {
                    // 清除敌人，保留警戒状态
                    AI.searchedEnemy = null;
                    AI.aimTarget = null;
                    
                    Debug.Log($"{LogTag} {MaidCharacter?.name} 战斗中但距离过远，清除敌人目标");
                }
            }
            
            SendMoveCommandToOwner();
            
            if (MaidCharacter != null)
            {
                MaidCharacter.PopText("距离过远，返回中...");
            }
            
            Debug.Log($"{LogTag} {MaidCharacter?.name} 进入强制跟随模式");
        }

        private void ExitForceFollowMode()
        {
            _isForceFollowing = false;
            _forceFollowTimer = 0f;
            
            if (MaidCharacter != null)
            {
                MaidCharacter.PopText("归队完成！");
            }
            
            Debug.Log($"{LogTag} {MaidCharacter?.name} 退出强制跟随模式");
        }

        private void TeleportToOwner()
        {
            if (MaidCharacter == null || MainOwner == null) return;

            float currentDist = Vector3.Distance(MaidCharacter.transform.position, MainOwner.transform.position);
            Debug.Log($"{LogTag} {MaidCharacter.name} 距离过远({currentDist:F1}m)，强制传送");

            if (AI != null)
            {
                AI.StopMove();
                AI.searchedEnemy = null;
                AI.aimTarget = null;
                AI.alert = false;
                AI.noticed = false;
            }

            MaidCharacter.transform.position = MainOwner.transform.position;

            if (AI != null && AI.transform.parent != MaidCharacter.transform)
            {
                AI.transform.position = MainOwner.transform.position;
            }

            _forceFollowTimer = 0f;

            Invoke(nameof(ResumeAIAfterTeleport), 0.2f);

            if (MaidCharacter != null)
            {
                MaidCharacter.PopText("强制传送！");
            }
        }

        private void ResumeAIAfterTeleport()
        {
            if (AI != null && MainOwner != null)
            {
                AI.leader = MainOwner;
                AI.patrolPosition = MainOwner.transform.position;
            }
        }
    }
}