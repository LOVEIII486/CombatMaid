using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.Core.MaidBehaviors
{
    /// <summary>
    /// 女仆手动移动模块
    /// </summary>
    public class MaidMovement : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidMovement]";
        
        private MaidController _controller;
        
        // 状态标记
        public bool IsActive { get; private set; } = false;

        // 计时器
        private float _failsafeTimer = 0f;
        private float _forceLockUntilTime = 0f; 

        private void Awake()
        {
            Debug.Log($"{LogTag} 组件已挂载: {gameObject.name}");
        }

        public void Initialize(MaidController controller)
        {
            _controller = controller;
        }

        public void OnUpdate()
        {
            if (!IsActive) return;

            _failsafeTimer -= Time.deltaTime;

            // 1. 强制锁定期间不进行到达判定
            if (Time.time < _forceLockUntilTime) return;

            // 2. 判断是否结束
            if (HasArrived() || _failsafeTimer <= 0)
            {
                if (_failsafeTimer <= 0) 
                    Debug.Log($"{LogTag} 移动超时，自动停止。");
                else 
                    Debug.Log($"{LogTag} AI报告已到达终点。");
                
                StopMove();
            }
        }

        /// <summary>
        /// 手动移动到指定位置
        /// </summary>
        public void MoveTo(Vector3 position)
        {
            if (_controller == null || _controller.AI == null) return;

            IsActive = true;
            _forceLockUntilTime = Time.time + 0.5f;
            _failsafeTimer = 15.0f; // 15秒超时保护

            // 开启和平模式
            _controller.SetPeaceMode(true);
            
            _controller.AI.StopMove();
            _controller.AI.MoveToPos(position);

            if (_controller.AI.CharacterMainControl != null)
            {
                _controller.AI.CharacterMainControl.PopText("移动中...");
            }
            
            Debug.Log($"{LogTag} 执行手动移动指令 -> {position}");
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false; 
            
            // 关闭和平模式
            if (_controller != null) 
                _controller.SetPeaceMode(false);
            
            if (_controller != null && _controller.AI != null)
            {
                _controller.AI.StopMove();
            }
        }

        /// <summary>
        /// 检查是否到达目标
        /// </summary>
        private bool HasArrived()
        {
            if (_controller == null || _controller.AI == null) return true;

            var ai = _controller.AI;
            
            // 1. 如果正在计算路径，肯定没到
            if (ai.WaitingForPathResult()) 
                return false;

            // 2. 如果官方属性显示已到达
            if (ai.ReachedEndOfPath()) 
                return true;

            // 3. 如果没有处于"移动"状态（且过了锁定时间），说明可能停了
            if (!ai.IsMoving()) 
                return true;

            return false;
        }
    }
}