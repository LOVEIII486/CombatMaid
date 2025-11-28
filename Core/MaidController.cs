using CombatMaid.Core.MaidBehaviors;
using UnityEngine;
using Duckov.Modding;

namespace CombatMaid.Core
{
    // 依然保留 RequireComponent 作为一种规范，但代码逻辑不再完全依赖它
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

            var ai = GetComponent<AICharacterController>();
            if (ai != null) ai.leader = player;

            // ==================== 修复核心 ====================
            // 显式获取或添加组件，不再完全依赖 RequireComponent 的自动行为
            _movement = GetComponent<MaidMovement>();
            if (_movement == null)
            {
                Debug.LogWarning($"[MaidController] 警告：MaidMovement 未自动添加，正在手动挂载...");
                _movement = gameObject.AddComponent<MaidMovement>();
            }

            if (_movement != null)
            {
                _movement.Initialize(ai);
                Debug.Log($"[MaidController] {profile.Config.CustomName} 初始化成功，Movement模块已就绪。");
            }
            else
            {
                Debug.LogError($"[MaidController] 严重错误：无法挂载 MaidMovement 组件！");
            }
            // ================================================
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
            else
            {
                // 如果此时还是空，说明初始化彻底失败了
                Debug.LogError("[MaidController] 无法移动：Movement 模块丢失！");
            }
        }
    }
}