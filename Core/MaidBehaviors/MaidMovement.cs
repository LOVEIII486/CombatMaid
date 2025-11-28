using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.Core.MaidBehaviors
{
    public class MaidMovement : MonoBehaviour
    {
        private AICharacterController _ai;
        private MaidController _controller;
        
        // 状态标记
        public bool IsActive { get; private set; } = false;

        // 计时器
        private float _failsafeTimer = 0f;
        private float _forceLockUntilTime = 0f; 

        private void Awake()
        {
            Debug.Log($"[MaidMovement] 组件已挂载: {gameObject.name}");
        }

        public void Initialize(AICharacterController ai, MaidController controller)
        {
            _ai = ai;
            _controller = controller;
        }

        public void OnUpdate()
        {
            if (!IsActive) return;

            _failsafeTimer -= Time.deltaTime;

            // 1. 强制锁定期间不进行到达判定 (给寻路计算留出时间)
            if (Time.time < _forceLockUntilTime) return;

            // 2. 判断是否结束
            if (HasArrived() || _failsafeTimer <= 0)
            {
                if (_failsafeTimer <= 0) Debug.Log("[MaidMovement] 移动超时，自动停止。");
                else Debug.Log("[MaidMovement] 已到达目标点。");
                
                StopMove();
            }
        }

        public void MoveTo(Vector3 position)
        {
            if (_ai == null) return;

            IsActive = true;
            _forceLockUntilTime = Time.time + 0.5f; // 给0.5秒缓冲让路径开始计算
            _failsafeTimer = 15.0f;

            // 开启和平模式 (抑制索敌)
            if (_controller != null) _controller.SetPeaceMode(true);

            // 直接调用官方 API
            _ai.StopMove();
            _ai.MoveToPos(position);

            if (_ai.CharacterMainControl != null)
            {
                _ai.CharacterMainControl.PopText("移动中...", 1.5f);
            }
            
            Debug.Log($"[MaidMovement] 执行移动指令 -> {position}");
        }

        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false; 
            
            // 关闭和平模式 (恢复战斗)
            if (_controller != null) _controller.SetPeaceMode(false);

            if (_ai != null) _ai.StopMove();
        }

        private bool HasArrived()
        {
            if (_ai == null) return true;

            // 核心逻辑：直接使用 AI_PathControl 的状态
            // 1. 如果正在计算路径，肯定没到
            if (_ai.WaitingForPathResult()) return false;

            // 2. 如果官方属性显示已到达
            if (_ai.ReachedEndOfPath()) return true;

            // 3. 如果甚至没有处于“移动”状态 (且过了锁定时间)，说明可能因为某种原因停了
            if (!_ai.IsMoving()) return true;

            return false;
        }
    }
}