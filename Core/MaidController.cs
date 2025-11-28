using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidBehaviors; 

namespace CombatMaid.Core
{
    [RequireComponent(typeof(MaidMovement))]
    public class MaidController : MonoBehaviour
    {
        private MaidProfile _profile;
        private CharacterMainControl _player;
        
        public MaidMovement Movement { get; private set; }

        // === 和平模式 (抑制索敌) ===
        public bool IsPeaceMode { get; private set; } = false;

        public void SetPeaceMode(bool enable)
        {
            IsPeaceMode = enable;
            if (enable)
            {
                var ai = GetComponent<AICharacterController>();
                if (ai != null)
                {
                    ai.searchedEnemy = null;
                    ai.aimTarget = null;
                    ai.alert = false;
                    ai.noticed = false;
                }
            }
        }
        // =========================

        public bool IsOverrideActive => Movement != null && Movement.IsActive;

        public void Initialize(MaidProfile profile, CharacterMainControl player)
        {
            _profile = profile;
            _player = player;

            var ai = GetComponent<AICharacterController>();
            if (ai == null) ai = GetComponentInChildren<AICharacterController>();
            
            if (ai == null)
            {
                Debug.LogError($"[MaidController] 严重错误：找不到 AICharacterController！");
                return;
            }
            ai.leader = player;
            ai.patrolRange = 100.0f; // 依然建议设大一点，防止原生逻辑干扰

            Movement = GetComponent<MaidMovement>();
            if (Movement == null) Movement = gameObject.AddComponent<MaidMovement>();
            Movement.Initialize(ai, this); 

            Debug.Log($"[MaidController] {profile.Config.CustomName} 初始化完毕。");
        }

        private void Update()
        {
            if (Movement != null) Movement.OnUpdate();
        }

        public void ForceMoveTo(Vector3 position)
        {
            if (Movement != null) Movement.MoveTo(position);
        }
    }
}