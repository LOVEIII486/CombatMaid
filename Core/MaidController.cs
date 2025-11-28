using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidBehaviors; // 引用新的命名空间

namespace CombatMaid.Core
{
    [RequireComponent(typeof(MaidMovement))]
    public class MaidController : MonoBehaviour
    {
        private MaidProfile _profile;
        private CharacterMainControl _player;
        private MaidMovement _movement;
        
        public bool IsOverrideActive => _movement != null && _movement.IsActive;

        public void Initialize(MaidProfile profile, CharacterMainControl player)
        {
            _profile = profile;
            _player = player;

            // 查找 AI 控制器 (支持子物体/父物体查找)
            var ai = GetComponent<AICharacterController>();
            if (ai == null) ai = GetComponentInChildren<AICharacterController>();
            
            if (ai == null)
            {
                Debug.LogError($"[MaidController] 严重错误：在 {gameObject.name} 上找不到 AICharacterController！");
                return;
            }
            ai.leader = player;

            // 挂载或获取 Movement
            _movement = GetComponent<MaidMovement>();
            if (_movement == null)
            {
                _movement = gameObject.AddComponent<MaidMovement>();
            }

            if (_movement != null)
            {
                _movement.Initialize(ai);
                Debug.Log($"[MaidController] {profile.Config.CustomName} 初始化完毕。");
            }
        }

        private void Update()
        {
            if (_movement != null) _movement.OnUpdate();
        }

        public void ForceMoveTo(Vector3 position)
        {
            if (_movement != null) _movement.MoveTo(position);
        }
    }
}