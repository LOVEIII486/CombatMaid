using UnityEngine;
using Pathfinding;

namespace CombatMaid.Core.MaidFSM.States
{
    public class State_Autonomous : MaidStateBase
    {
        private const float IdleThreshold = 2.5f;
        private const float PatrolRadiusMax = 8.0f;
        private const float ChangePosInterval = 4.0f;

        private Vector3 _lastOwnerPos;
        private float _idleTimer;
        private float _nextPatrolMoveTime;
        private bool _isPatrolling;
        private Vector3? _currentPatrolPoint;

        // 缓存原始参数
        private float _originForceTraceDist;
        private float _originPatrolRange;
        private CharacterMainControl _originLeader; // 缓存原生领袖

        public override void Enter()
        {
            SetNativeBrainActive(true);
            _idleTimer = 0f;
            _nextPatrolMoveTime = 0f;
            _isPatrolling = false;
            _currentPatrolPoint = null;

            if (Controller.MainOwner != null)
                _lastOwnerPos = Controller.MainOwner.transform.position;

            if (Controller.AI != null)
            {
                _originForceTraceDist = Controller.AI.forceTracePlayerDistance;
                _originPatrolRange = Controller.AI.patrolRange;
                _originLeader = Controller.AI.leader; // 备份玩家引用
            }
        }

        public override void Update()
        {
            if (Controller.MainOwner == null || Controller.AI == null) return;

            Controller.AIAssistant.OnTick();

            // 1. 距离检查：超过 Mod 安全距离则切换到强制跟随
            if (Controller.IsTooFarFromOwner())
            {
                ResetNativeParameters();
                Machine.ChangeState<State_ForceFollow>();
                return;
            }

            Vector3 ownerCurrentPos = Controller.MainOwner.transform.position;
            bool isPlayerMoving = (ownerCurrentPos - _lastOwnerPos).sqrMagnitude > 1.44f;
            
            if (isPlayerMoving)
            {
                _lastOwnerPos = ownerCurrentPos; 
                ResetNativeParameters();
                _idleTimer = 0f;
                Controller.ManualMoveTarget = null;
            }
            else
            {
                if (_isPatrolling && _currentPatrolPoint.HasValue)
                {
                    Controller.AI.patrolPosition = _currentPatrolPoint.Value;
                    Controller.AI.patrolRange = 1.0f;
                }

                _idleTimer += Time.deltaTime;
                if (_idleTimer > IdleThreshold)
                {
                    HandleIdlePatrol(ownerCurrentPos);
                }
            }
        }

        private void HandleIdlePatrol(Vector3 centerPos)
        {
            if (Time.time < _nextPatrolMoveTime) return;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 roughTarget = centerPos + (dir * Random.Range(4f, PatrolRadiusMax));

            if (AstarPath.active != null)
            {
                var nearest = AstarPath.active.GetNearest(roughTarget, NNConstraint.Default);
                if (nearest.node != null && nearest.node.Walkable)
                {
                    Vector3 validPos = (Vector3)nearest.node.position;
                    
                    _isPatrolling = true;
                    _currentPatrolPoint = validPos;
                    Controller.ManualMoveTarget = validPos;

                    // --- 核心修复：彻底阻断跟随欲望 ---
                    // 1. 暂时移除原生领袖引用，使其失去“归巢”目标
                    // 仅在非战斗状态下移除，确保战斗时能识别队友
                    if (!Controller.AI.noticed && !Controller.AI.alert) 
                    {
                        Controller.AI.leader = null;
                    }

                    // 2. 设置原生巡逻参数
                    Controller.AI.patrolPosition = validPos;
                    Controller.AI.patrolRange = 1.0f;
                    Controller.AI.forceTracePlayerDistance = 25.0f;

                    // 3. 执行移动指令
                    Controller.AI.MoveToPos(validPos); 
                    
                    //CMDebug.Log($"[自主巡逻] 已重置原生锚点至: {validPos}");
                }
            }

            _nextPatrolMoveTime = Time.time + ChangePosInterval + Random.Range(0f, 3.0f);
        }

        private void ResetNativeParameters()
        {
            if (Controller.AI == null || !_isPatrolling) return;

            // 恢复原始参数和领袖引用
            Controller.AI.leader = _originLeader ?? Controller.MainOwner;
            Controller.AI.forceTracePlayerDistance = _originForceTraceDist;
            Controller.AI.patrolRange = _originPatrolRange;
            
            // 清除当前路径，让其立刻重新评估并跟随玩家
            Controller.AI.MoveToPos(Controller.MainOwner.transform.position);
            
            _isPatrolling = false;
            _currentPatrolPoint = null;
        }

        public override void Exit()
        {
            ResetNativeParameters();
            base.Exit();
        }
    }
}