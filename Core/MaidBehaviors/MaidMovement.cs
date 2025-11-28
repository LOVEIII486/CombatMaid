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
        
        // 记录移动目标，用于在没有 Agent 时的距离检测
        private Vector3 _targetPosition;
        
        // 状态标记
        public bool IsActive { get; private set; } = false;

        // 计时器
        private float _failsafeTimer = 0f;
        private float _warmupTimer = 0f;

        private void Awake()
        {
            Debug.Log($"[MaidMovement] 组件已挂载到物体: {gameObject.name}");
        }

        // 初始化依赖
        public void Initialize(AICharacterController ai)
        {
            _ai = ai;
            
            // 尝试获取 NavMeshAgent，但如果不存也不视为致命错误（可能使用的是 A* Pathfinding）
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = GetComponentInParent<NavMeshAgent>();
            if (_agent == null && ai != null && ai.CharacterMainControl != null)
                _agent = ai.CharacterMainControl.GetComponent<NavMeshAgent>();

            if (_agent != null)
            {
                _agent.autoRepath = false; 
                Debug.Log($"[MaidMovement] 检测到 NavMeshAgent，已接管控制。");
            }
            else
            {
                // 改为 LogWarning，因为这可能是正常的（游戏使用其他寻路系统）
                Debug.LogWarning($"[MaidMovement] 未找到 NavMeshAgent，将使用通用距离检测模式。");
            }
        }

        public void OnUpdate()
        {
            if (!IsActive) return;

            // 1. 启动缓冲期
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
                if (_failsafeTimer <= 0) Debug.Log("[MaidMovement] 移动超时，自动停止。");
                else Debug.Log("[MaidMovement] 已到达目标点。");
                
                StopMove();
            }
        }

        public void MoveTo(Vector3 position)
        {
            if (_ai == null)
            {
                Debug.LogError("[MaidMovement] 移动失败：AI Controller 为空！");
                return;
            }

            IsActive = true;
            _targetPosition = position; // 记录目标点
            _failsafeTimer = 15.0f;     // 15秒最大移动时间
            _warmupTimer = 0.2f;        // 0.2秒缓冲，防止刚开始移动就被误判为到达

            // 1. 停止当前动作
            _ai.StopMove();

            // 2. 如果有 Agent，进行配置
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.autoRepath = false;
                _agent.SetDestination(position);
            }

            // 3. 调用 AI 接口移动 (兼容 A* Pathfinding)
            _ai.MoveToPos(position);

            if (_ai.CharacterMainControl != null)
            {
                _ai.CharacterMainControl.PopText("收到指令！", 1.5f);
            }
            
            Debug.Log($"[MaidMovement] 执行移动指令 -> {position}");
        }

        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false; // 只有这里设为 false，Harmony 才会释放拦截
            
            if (_ai != null) _ai.StopMove();

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }

        private bool HasArrived()
        {
            // 策略 A: 优先使用 NavMeshAgent 判断
            if (_agent != null && _agent.isOnNavMesh)
            {
                if (_agent.pathPending) return false;
                if (_agent.remainingDistance <= _agent.stoppingDistance + 0.5f)
                {
                    if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f) return true;
                }
                return false;
            }

            // 策略 B: 通用距离判断 (适用于 A* 或无 Agent 情况)
            // 忽略 Y 轴高度差，只计算水平距离，防止地形导致无法到达
            Vector3 currentPos = transform.position;
            Vector3 targetPos = _targetPosition;
            currentPos.y = 0;
            targetPos.y = 0;

            float dist = Vector3.Distance(currentPos, targetPos);
            
            // 当距离小于 1.5 米时认为到达
            return dist < 1.5f;
        }
    }
}