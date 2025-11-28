using UnityEngine;
using UnityEngine.AI;
using Duckov.Modding;

namespace CombatMaid.Core.MaidBehaviors
{
    /// <summary>
    /// 专门负责处理移动指令、寻路逻辑和强制位移状态
    /// </summary>
    public class MaidMovement : MonoBehaviour
    {
        private AICharacterController _ai;
        private NavMeshAgent _agent;
        
        // 状态标记
        public bool IsActive { get; private set; } = false;

        // 计时器
        private float _failsafeTimer = 0f;
        private float _warmupTimer = 0f;

        private void Awake()
        {
            // 确认组件确实被挂载到了物体上
            Debug.Log($"[MaidMovement] 组件已挂载到物体: {gameObject.name}");
        }

        // 初始化依赖
        public void Initialize(AICharacterController ai)
        {
            _ai = ai;
            _agent = ai.GetComponent<NavMeshAgent>();
            
            if (_agent != null)
            {
                _agent.autoRepath = false; // 禁止原生AI自动重算路径覆盖我们的指令
                Debug.Log($"[MaidMovement] NavMeshAgent 获取成功 (OnNavMesh: {_agent.isOnNavMesh})");
            }
            else
            {
                Debug.LogError($"[MaidMovement] 严重错误：找不到 NavMeshAgent！");
            }
        }

        // 每帧更新移动状态
        public void OnUpdate()
        {
            if (!IsActive) return;

            // 1. 启动缓冲期 (给NavMesh计算路径的时间)
            if (_warmupTimer > 0)
            {
                _warmupTimer -= Time.deltaTime;
                return;
            }

            // 2. 超时保险
            _failsafeTimer -= Time.deltaTime;

            // 3. 判断是否结束
            if (HasArrived() || _failsafeTimer <= 0)
            {
                if (_failsafeTimer <= 0) Debug.Log("[MaidMovement] 移动超时，强制停止。");
                StopMove();
            }
        }

        /// <summary>
        /// 执行强制移动
        /// </summary>
        public void MoveTo(Vector3 position)
        {
            if (_ai == null || _agent == null)
            {
                Debug.LogError("[MaidMovement] 移动失败：依赖项为空！");
                return;
            }

            IsActive = true;
            _failsafeTimer = 15.0f; // 15秒后强制放弃
            _warmupTimer = 0.5f;    // 0.5秒缓冲

            // 停止原生动作
            _ai.StopMove();

            // 激活 Agent 并寻路
            // 增加判断：只有在 Agent 处于 NavMesh 上时才能 SetDestination
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                bool pathFound = _agent.SetDestination(position);
                if (!pathFound)
                {
                    Debug.LogWarning($"[MaidMovement] SetDestination 返回 false，目标点可能不可达: {position}");
                }
            }
            else
            {
                Debug.LogWarning($"[MaidMovement] Agent 不在 NavMesh 上，尝试 Warp 修正...");
                // 尝试修复：如果不在导航网格上，尝试瞬移到最近的网格点
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(hit.position);
                    _agent.SetDestination(position);
                    Debug.Log($"[MaidMovement] Warp 修正成功，继续移动。");
                }
                else
                {
                    Debug.LogError($"[MaidMovement] Agent 完全脱离导航网格，无法移动！");
                    IsActive = false;
                    return;
                }
            }

            // 同步动画系统
            _ai.MoveToPos(position);

            if (_ai.CharacterMainControl != null)
            {
                _ai.CharacterMainControl.PopText("收到指令！", 1.5f);
            }
            
            Debug.Log($"[MaidMovement] 开始移动至 {position}");
        }

        /// <summary>
        /// 停止强制移动，释放控制权
        /// </summary>
        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false;
            
            // 重置路径，避免切换回原生AI时还在往旧地方跑
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }

        private bool HasArrived()
        {
            if (_agent == null || !_agent.isOnNavMesh) return true;
            if (_agent.pathPending) return false;

            if (_agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
            {
                if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}