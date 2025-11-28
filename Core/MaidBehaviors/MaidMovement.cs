using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.Core.MaidBehaviors
{
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
            // 移除 TryGetAgent，不需要再手动找 Agent 了
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
                if (_failsafeTimer <= 0) Debug.Log($"{LogTag}  移动超时，自动停止。");
                else Debug.Log($"{LogTag}  AI报告已到达终点。");
                
                StopMove();
            }
        }

        public void MoveTo(Vector3 position)
        {
            // 如果控制器或 AI 还没准备好，直接返回
            if (_controller == null || _controller.AI == null) return;

            IsActive = true;
            _forceLockUntilTime = Time.time + 0.5f; // 给0.5秒缓冲让路径开始计算
            _failsafeTimer = 15.0f;

            // 开启和平模式 (激活 Harmony 拦截，这是防止乱跑的核心)
            _controller.SetPeaceMode(true);

            // 直接调用官方 API
            // StopMove 会重置内部状态，MoveToPos 会启动内部寻路
            _controller.AI.StopMove();
            _controller.AI.MoveToPos(position);

            if (_controller.AI.CharacterMainControl != null)
            {
                _controller.AI.CharacterMainControl.PopText("移动中...");
            }
            
            Debug.Log($"{LogTag}  执行移动指令 -> {position}");
        }

        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false; 
            
            // 关闭和平模式 (恢复战斗/跟随)
            if (_controller != null) _controller.SetPeaceMode(false);

            if (_controller != null && _controller.AI != null)
            {
                _controller.AI.StopMove();
            }
        }

        private bool HasArrived()
        {
            if (_controller == null || _controller.AI == null) return true;

            var ai = _controller.AI;

            // 核心逻辑：完全信任 AICharacterController 的状态
            // 1. 如果正在计算路径，肯定没到
            if (ai.WaitingForPathResult()) return false;

            // 2. 如果官方属性显示已到达
            if (ai.ReachedEndOfPath()) return true;

            // 3. 如果甚至没有处于“移动”状态 (且过了锁定时间)，说明可能因为某种原因停了
            if (!ai.IsMoving()) return true;

            return false;
        }
    }
}