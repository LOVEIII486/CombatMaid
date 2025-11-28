using UnityEngine;
using UnityEngine.AI;
using Duckov.Modding;

namespace CombatMaid.MaidBehaviors
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

        // 初始化依赖
        public void Initialize(AICharacterController ai)
        {
            _ai = ai;
            _agent = ai.GetComponent<NavMeshAgent>();
            
            if (_agent != null)
            {
                _agent.autoRepath = false; // 禁止原生AI自动重算路径覆盖我们的指令
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
                StopMove();
            }
        }

        /// <summary>
        /// 执行强制移动
        /// </summary>
        public void MoveTo(Vector3 position)
        {
            if (_ai == null || _agent == null) return;

            IsActive = true;
            _failsafeTimer = 15.0f; // 15秒后强制放弃
            _warmupTimer = 0.5f;    // 0.5秒缓冲

            // 停止原生动作
            _ai.StopMove();

            // 激活 Agent 并寻路
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(position);
            }

            // 同步动画系统
            _ai.MoveToPos(position);

            _ai.CharacterMainControl.PopText("收到指令！", 1.5f);
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
            
            // 可选：反馈
            // _ai.CharacterMainControl.PopText("到位。", 1f);
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