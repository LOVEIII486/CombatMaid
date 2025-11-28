using CombatMaid.Core.MaidBehaviors;
using UnityEngine;

namespace CombatMaid.Core
{
    // 尽管保留 RequireComponent，我们在代码中也会手动保险
    [RequireComponent(typeof(MaidMovement))]
    public class MaidController : MonoBehaviour
    {
        private Core.MaidProfile _profile;
        private CharacterMainControl _player;
        
        // === 模块引用 ===
        private MaidMovement _movement;
        
        // === 对外接口 ===
        public bool IsOverrideActive => _movement != null && _movement.IsActive;

        public void Initialize(Core.MaidProfile profile, CharacterMainControl player)
        {
            _profile = profile;
            _player = player;

            // 1. 安全获取 AI 控制器
            var ai = GetComponent<AICharacterController>();
            if (ai == null)
            {
                Debug.LogError("[MaidController] 严重错误：物体上找不到 AICharacterController，初始化中止。");
                return;
            }
            
            ai.leader = player;

            // 2. 安全获取或添加 Movement 模块 (修复空引用的关键)
            _movement = GetComponent<MaidMovement>();
            if (_movement == null)
            {
                // 如果 RequireComponent 没生效，我们手动加一个
                _movement = gameObject.AddComponent<MaidMovement>();
                Debug.LogWarning("[MaidController] 如果 RequireComponent 没生效，我们手动加一个");
            }

            // 3. 初始化子模块
            if (_movement != null)
            {
                _movement.Initialize(ai);
            }
            else
            {
                Debug.LogError("[MaidController] 无法挂载 MaidMovement 组件！");
            }
        }

        private void Update()
        {
            // 每一帧驱动子模块运行
            if (_movement != null)
            {
                _movement.OnUpdate();
            }
        }

        /// <summary>
        /// 接收来自 Manager 的移动指令，转发给 Movement 模块
        /// </summary>
        public void ForceMoveTo(Vector3 position)
        {
            if (_movement != null)
            {
                _movement.MoveTo(position);
            }
        }
    }
}