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

        // ==================== 防卡死设置 ====================
        
        [Header("Anti-Stuck Settings")]
        public float ForceFollowDistance = 15.0f;
        public float TeleportDistance = 30.0f; 
        public float TeleportTimeout = 8.0f;
        public float SafeDistanceToResumeCombat = 10.0f;
        
        private float _forceFollowTimer = 0f;
        private bool _isForceFollowing = false;

        // ==================== 状态控制 ====================
        
        // 简化：不再使用和平模式！
        public bool IsPeaceMode { get; private set; } = false;
        
        // Harmony 只在手动移动时拦截
        public bool IsOverrideActive => Movement != null && Movement.IsActive;

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

            // 设置 leader - 这是关键！
            AI.leader = player;
            AI.patrolRange = 100.0f;
            AI.patrolPosition = player.transform.position;

            Movement = GetComponent<MaidMovement>();
            if (Movement == null) Movement = gameObject.AddComponent<MaidMovement>();
            Movement.Initialize(this); 

            Debug.Log($"{LogTag} {profile.Config.CustomName} 初始化完毕 (使用官方优先级系统)");
        }

        private void OnDestroy()
        {
            if (AI != null && _maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Remove(AI);
            }
        }

        /// <summary>
        /// 简化的和平模式 - 只清除目标，保留警戒状态
        /// </summary>
        public void SetPeaceMode(bool enable)
        {
            IsPeaceMode = enable;
            
            if (enable && AI != null)
            {
                // 只清除战斗目标，不清除警戒状态
                AI.searchedEnemy = null;
                AI.aimTarget = null;
                // 保留 alert 和 noticed，让 AI 保持警觉
                
                Debug.Log($"{LogTag} 和平模式：清除战斗目标，保持警戒状态");
            }
        }

        private void Update()
        {
            if (Movement != null) Movement.OnUpdate();
            UpdateSmartFollow();
            
            // 持续更新巡逻位置到主人位置
            if (AI != null && MainOwner != null)
            {
                AI.patrolPosition = MainOwner.transform.position;
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
                        // 使用官方方式：更新 patrolPosition + MoveToPos
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

        /// <summary>
        /// 优雅的方式：使用官方的 patrolPosition 系统
        /// </summary>
        private void SendMoveCommandToOwner()
        {
            if (AI == null || MainOwner == null) return;
            
            Vector3 targetPos = MainOwner.transform.position;
            
            // 方法1：更新巡逻位置（行为树会自动处理）
            AI.patrolPosition = targetPos;
            
            // 方法2：如果 AI 在战斗，主动发送移动指令打断
            if (AI.searchedEnemy != null)
            {
                AI.StopMove();
                AI.MoveToPos(targetPos);
            }
            
            Debug.Log($"{LogTag} {MaidCharacter?.name} 更新跟随目标 (距离: {Vector3.Distance(MaidCharacter.transform.position, targetPos):F1}m)");
        }

        /// <summary>
        /// 进入强制跟随模式 - 使用温和的方式
        /// </summary>
        private void EnterForceFollowMode()
        {
            _isForceFollowing = true;
            _forceFollowTimer = 0f;
            
            if (AI != null)
            {
                bool hasEnemy = AI.searchedEnemy != null;
                
                if (hasEnemy)
                {
                    // 方案A：清除敌人，保留警戒状态
                    AI.searchedEnemy = null;
                    AI.aimTarget = null;
                    // 保留 alert 和 noticed
                    
                    Debug.Log($"{LogTag} {MaidCharacter?.name} 战斗中但距离过远，清除敌人目标");
                }
            }
            
            // 发送移动指令
            SendMoveCommandToOwner();
            
            if (MaidCharacter != null)
            {
                MaidCharacter.PopText("距离过远，返回中...");
            }
            
            Debug.Log($"{LogTag} {MaidCharacter?.name} 进入强制跟随模式");
        }

        /// <summary>
        /// 退出强制跟随模式
        /// </summary>
        private void ExitForceFollowMode()
        {
            _isForceFollowing = false;
            _forceFollowTimer = 0f;
            
            // 不需要做任何特殊处理
            // AI 会自然恢复索敌
            
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
                // 传送时清除战斗状态
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