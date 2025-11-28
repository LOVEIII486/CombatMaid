using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.MaidBehaviors
{
    // 强制依赖 Movement 组件
    [RequireComponent(typeof(MaidMovement))]
    public class MaidController : MonoBehaviour
    {
        private Core.MaidProfile _profile;
        private CharacterMainControl _player;
        
        // === 模块引用 ===
        private MaidMovement _movement;
        
        // === 对外接口 ===
        // 供 HarmonyPatch 调用，判断是否需要屏蔽原生AI
        public bool IsOverrideActive => _movement != null && _movement.IsActive;

        public void Initialize(Core.MaidProfile profile, CharacterMainControl player)
        {
            _profile = profile;
            _player = player;

            var ai = GetComponent<AICharacterController>();
            if (ai != null) ai.leader = player;

            // 初始化子模块
            _movement = GetComponent<MaidMovement>();
            _movement.Initialize(ai);
            
            // 未来可以在这里初始化 _combat, _interaction 等模块
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