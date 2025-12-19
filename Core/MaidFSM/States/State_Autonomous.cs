using UnityEngine;
using CombatMaid.Core;
using ItemStatsSystem;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 自主模式：把控制权交给原生AI，闲置时分散巡逻
    /// </summary>
    public class State_Autonomous : MaidStateBase
    {
        // 配置参数
        private const float IdleThreshold = 3.0f;     // 判定为闲置的时间
        private const float PatrolRadiusMin = 2.0f;  // 最小巡逻半径
        private const float PatrolRadiusMax = 5.0f;  // 最大巡逻半径
        private const float ChangePosInterval = 5.0f;// 闲置时多久换一次位置

        // 运行时变量
        private Vector3 _lastOwnerPos;
        private float _idleTimer;
        private float _nextPatrolMoveTime;

        public override void Enter()
        {
            SetNativeBrainActive(true);
            
            _idleTimer = 0f;
            _nextPatrolMoveTime = 0f;

            if (Controller.MainOwner != null)
            {
                _lastOwnerPos = Controller.MainOwner.transform.position;
                // 进入状态时先跟随一次
                UpdatePatrolPosition(Controller.MainOwner.transform.position);
            }
        }

        public override void Update()
        {
            if (Controller.MainOwner == null || Controller.AI == null) return;
            
            Controller.AIAssistant.OnTick();

            // 如果离得太远，优先切换到强制跟随
            if (Controller.IsTooFarFromOwner())
            {
                Machine.ChangeState<State_ForceFollow>();
                return;
            }

            // 检测玩家是否移动
            Vector3 ownerCurrentPos = Controller.MainOwner.transform.position;
            bool isPlayerMoving = (ownerCurrentPos - _lastOwnerPos).sqrMagnitude > 0.01f;
            
            _lastOwnerPos = ownerCurrentPos;

            if (isPlayerMoving)
            {
                // 玩家移动
                _idleTimer = 0f;
                // 紧跟模式：直接设置目标为玩家位置
                UpdatePatrolPosition(ownerCurrentPos);
            }
            else
            {
                // 玩家静止
                _idleTimer += Time.deltaTime;

                if (_idleTimer > IdleThreshold)
                {
                    // 超过3秒，开始分散闲逛
                    HandleIdlePatrol(ownerCurrentPos);
                }
                else
                {
                    // 3秒内保持当前位置
                    UpdatePatrolPosition(ownerCurrentPos);
                }
            }
        }

        /// <summary>
        /// 处理闲置时的巡逻逻辑
        /// </summary>
        private void HandleIdlePatrol(Vector3 centerPos)
        {
            if (Time.time >= _nextPatrolMoveTime)
            {
                Vector2 randomCircle = Random.insideUnitCircle;
                Vector3 offset = new Vector3(randomCircle.x, 0, randomCircle.y);
                float distance = Random.Range(PatrolRadiusMin, PatrolRadiusMax);
                Vector3 roughTargetPos = centerPos + (offset.normalized * distance);

                NavMeshHit hit;
                if (NavMesh.SamplePosition(roughTargetPos, out hit, 2.0f, NavMesh.AllAreas))
                {
                    UpdatePatrolPosition(hit.position);
                    // CMDebug.Log($"[{Controller.name}] 闲逛目标已修正: {roughTargetPos} -> {hit.position}");
                }
                else
                {
                    // CMDebug.LogWarning($"[{SkillName}] 随机点无效，放弃移动");
                }
                _nextPatrolMoveTime = Time.time + ChangePosInterval + Random.Range(0f, 2.0f);
            }
        }

        /// <summary>
        /// 封装设置AI巡逻点的方法
        /// </summary>
        private void UpdatePatrolPosition(Vector3 pos)
        {
            if (Controller.AI != null)
            {
                Controller.AI.patrolPosition = pos;
            }
        }
    }
}