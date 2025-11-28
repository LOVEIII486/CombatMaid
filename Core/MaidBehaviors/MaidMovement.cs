using UnityEngine;
using UnityEngine.AI;
using Duckov.Modding;

namespace CombatMaid.Core.MaidBehaviors
{
    public class MaidMovement : MonoBehaviour
    {
        // 现在直接持有 Controller，通过它访问 AI
        private MaidController _controller;
        private NavMeshAgent _agent; 
        
        public bool IsActive { get; private set; } = false;
        private float _failsafeTimer = 0f;
        private float _forceLockUntilTime = 0f; 

        // 参数简化，数据从 controller 拿
        public void Initialize(MaidController controller)
        {
            _controller = controller;
            TryGetAgent(); 
        }

        private void TryGetAgent()
        {
            if (_agent != null) return;
            if (_controller == null || _controller.AI == null) return;

            // 优化：优先从 cached AI 中找
            _agent = _controller.AI.GetComponent<NavMeshAgent>();
            
            // 备选方案
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_agent == null) _agent = GetComponentInParent<NavMeshAgent>();
            
            // 从 CharacterMainControl 找 (利用已缓存的引用)
            if (_agent == null && _controller.AI.CharacterMainControl != null)
                _agent = _controller.AI.CharacterMainControl.GetComponent<NavMeshAgent>();

            if (_agent != null) _agent.autoRepath = false;
        }

        public void OnUpdate()
        {
            if (_agent == null) TryGetAgent();

            if (!IsActive) return;

            _failsafeTimer -= Time.deltaTime;
            if (Time.time < _forceLockUntilTime) return;

            bool arrived = false;
            // 通过 _controller.AI 访问
            var ai = _controller.AI; 

            if (ai != null)
            {
                if (ai.WaitingForPathResult()) arrived = false;
                else if (ai.HasPath() && ai.ReachedEndOfPath()) arrived = true;
                else if (!ai.HasPath()) arrived = true; 
            }
            
            if (arrived || _failsafeTimer <= 0)
            {
                if (_failsafeTimer <= 0) Debug.Log("[MaidMovement] 移动超时，自动停止。");
                else Debug.Log("[MaidMovement] AI报告已到达终点。");
                StopMove();
            }
        }

        public void MoveTo(Vector3 position)
        {
            if (_controller == null || _controller.AI == null) return;

            IsActive = true;
            _forceLockUntilTime = Time.time + 0.5f; 
            _failsafeTimer = 15.0f;

            _controller.SetPeaceMode(true);

            _controller.AI.StopMove();

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.autoRepath = false;
                _agent.SetDestination(position);
            }

            _controller.AI.MoveToPos(position);

            if (_controller.AI.CharacterMainControl != null)
            {
                _controller.AI.CharacterMainControl.PopText("移动中...", 1.5f);
            }
            
            Debug.Log($"[MaidMovement] 执行移动指令 -> {position}");
        }

        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false; 
            _controller.SetPeaceMode(false);

            if (_controller.AI != null) _controller.AI.StopMove();

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }
    }
}