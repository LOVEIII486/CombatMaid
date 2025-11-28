using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.Core.MaidBehaviors
{
    /// <summary>
    /// 女仆手动移动模块
    /// 用于玩家手动指挥女仆移动（G键）
    /// 使用 A* Pathfinding 的原生 API
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

            // 1. 强制锁定期间不进行到达判定（给 A* 时间计算路径）
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
        /// 手动移动到指定位置（玩家指令）
        /// </summary>
        public void MoveTo(Vector3 position)
        {
            // 如果控制器或 AI 还没准备好，直接返回
            if (_controller == null || _controller.AI == null) return;

            IsActive = true;
            _forceLockUntilTime = Time.time + 0.5f; // 给0.5秒缓冲让A*计算路径
            _failsafeTimer = 15.0f; // 15秒超时保护

            // 开启和平模式（防止战斗打断手动移动）
            _controller.SetPeaceMode(true);

            // 使用 A* Pathfinding 的原生 API
            _controller.AI.StopMove();      // 停止当前移动
            _controller.AI.MoveToPos(position); // 发起新的寻路请求

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
            
            // 关闭和平模式（恢复战斗/跟随）
            if (_controller != null) 
                _controller.SetPeaceMode(false);

            // 停止 A* 寻路
            if (_controller != null && _controller.AI != null)
            {
                _controller.AI.StopMove();
            }
        }

        /// <summary>
        /// 检查是否到达目标（使用 A* Pathfinding 的原生状态）
        /// </summary>
        private bool HasArrived()
        {
            if (_controller == null || _controller.AI == null) return true;

            var ai = _controller.AI;

            // 核心逻辑：完全信任 AICharacterController 的状态
            
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