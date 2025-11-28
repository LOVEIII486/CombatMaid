using UnityEngine;
using UnityEngine.AI;

namespace CombatMaid.Core.MaidBehaviors
{
    /// <summary>
    /// 专门负责处理移动指令、寻路逻辑和强制位移状态
    /// </summary>
    public class MaidMovement : MonoBehaviour
    {
        private AICharacterController _ai;
        private NavMeshAgent _agent;
        
        public bool IsActive { get; private set; } = false;

        private float _failsafeTimer = 0f;
        private float _warmupTimer = 0f;

        public void Initialize(AICharacterController ai)
        {
            _ai = ai;
            _agent = ai.GetComponent<NavMeshAgent>();
            
            if (_agent != null)
            {
                _agent.autoRepath = false; 
            }
        }

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

        public void MoveTo(Vector3 position)
        {
            if (_ai == null || _agent == null) return;

            IsActive = true;
            _failsafeTimer = 15.0f; 
            _warmupTimer = 0.5f;

            // === 关键修改：执行顺序调整 ===
            
            // 1. 停止当前原生动作 (打断当前行为树)
            _ai.StopMove();

            // 2. 调用原生接口 (这一步通常会设置动画状态，让角色"准备"移动)
            _ai.MoveToPos(position);

            // 3. 强制覆盖 NavMeshAgent (这是真正驱动位移的引擎)
            // 必须在 MoveToPos 之后调用，防止 MoveToPos 内部重置路径或停止 Agent
            if (_agent.isOnNavMesh)
            {
                _agent.autoRepath = false; // 再次确保关闭自动重算
                _agent.isStopped = false;  // 确保油门是踩下的
                _agent.SetDestination(position);
            }

            _ai.CharacterMainControl.PopText("收到指令！", 1.5f);
            Debug.Log($"[MaidMovement] 强制移动启动 -> {position}");
        }

        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false;
            
            if (_agent != null && _agent.isOnNavMesh)
            {
                if (!_agent.isStopped) _agent.ResetPath();
            }
            
            Debug.Log("[MaidMovement] 强制移动结束，释放控制权。");
        }

        private bool HasArrived()
        {
            if (_agent == null || !_agent.isOnNavMesh) return true;
            
            // 路径还在计算中，不算到达
            if (_agent.pathPending) return false;

            // 剩余距离小于停止距离
            if (_agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
            {
                // 且没有正在进行的路径规划，或者速度已经很慢了
                if (!_agent.hasPath || _agent.velocity.sqrMagnitude <= 0.1f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}