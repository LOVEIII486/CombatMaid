using UnityEngine;
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
        private const float IdleThreshold = 2.0f;     
        private const float PatrolRadiusMin = 3.0f;  
        private const float PatrolRadiusMax = 7.0f;  
        private const float ChangePosInterval = 4.0f;
        private const float ReloadCheckInterval = 5.0f;

        // 运行时变量
        private Vector3 _lastOwnerPos;
        private float _idleTimer;
        private float _nextPatrolMoveTime;
        private float _nextReloadCheckTime;

        public override void Enter()
        {
            SetNativeBrainActive(true);
            _idleTimer = 0f;
            _nextPatrolMoveTime = 0f;
            _nextReloadCheckTime = 0f;

            if (Controller.MainOwner != null)
            {
                _lastOwnerPos = Controller.MainOwner.transform.position;
                UpdatePatrolPosition(Controller.MainOwner.transform.position);
            }
        }

        public override void Update()
        {
            if (Controller.MainOwner == null || Controller.AI == null) return;
            
            Controller.AIAssistant.OnTick();

            if (Controller.IsTooFarFromOwner())
            {
                Machine.ChangeState<State_ForceFollow>();
                return;
            }

            Vector3 ownerCurrentPos = Controller.MainOwner.transform.position;
            bool isPlayerMoving = (ownerCurrentPos - _lastOwnerPos).sqrMagnitude > 0.01f;
            _lastOwnerPos = ownerCurrentPos;

            if (isPlayerMoving)
            {
                _idleTimer = 0f;
                UpdatePatrolPosition(ownerCurrentPos);
            }
            else
            {
                _idleTimer += Time.deltaTime;
                if (_idleTimer > IdleThreshold)
                {
                    if (Time.time >= _nextReloadCheckTime)
                    {
                        TryAutoReload();
                        _nextReloadCheckTime = Time.time + ReloadCheckInterval;
                    }

                    HandleIdlePatrol(ownerCurrentPos);
                }
                else
                {
                    UpdatePatrolPosition(ownerCurrentPos);
                }
            }
        }

        /// <summary>
        /// 换弹
        /// </summary>
        private void TryAutoReload()
        {
            var mc = Controller.MaidCharacter;
            if (mc == null) return;

            var gunAgent = mc.GetGun();
            if (gunAgent == null) return;

            var gunSetting = gunAgent.GunItemSetting;
            if (gunSetting == null) return;
            
            if (!gunSetting.IsFull() && mc.CurrentAction == null && mc.CanUseHand())
            {
                bool success = mc.TryToReload();
                if (success)
                {
                    string maidName = mc.characterPreset != null ? mc.characterPreset.DisplayName : "战斗女仆";
                    CMDebug.Log($"[{maidName}] 闲置中，检测到弹药不满，开始自动换弹...");
                }
            }
        }

        /// <summary>
        /// 闲置时巡逻
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
        /// 设置AI巡逻点
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