using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidBehaviors; 

namespace CombatMaid.Core
{
    [RequireComponent(typeof(MaidMovement))]
    public class MaidController : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidController]";
        
        // 全局注册表
        private static readonly Dictionary<AICharacterController, MaidController> _maidRegistry 
            = new Dictionary<AICharacterController, MaidController>();

        /// <summary>
        /// 通过原生AI找到对应的 MaidController
        /// </summary>
        public static MaidController GetMaid(AICharacterController ai)
        {
            if (ai == null) return null;
            return _maidRegistry.GetValueOrDefault(ai);
        }
        
        /// <summary>
        /// 1. 女仆的AI控制器 (大脑) - 控制寻路、索敌、感知
        /// </summary>
        public AICharacterController AI { get; private set; }

        /// <summary>
        /// 2. 女仆的角色主控制器 (身体) - 控制血量、装备、动作、死亡
        /// </summary>
        public CharacterMainControl MaidCharacter => AI != null ? AI.CharacterMainControl : null;

        /// <summary>
        /// 3. 女仆的主人/队长 (玩家)
        /// </summary>
        public CharacterMainControl MainOwner { get; private set; }

        /// <summary>
        /// 4. 移动逻辑模块
        /// </summary>
        public MaidMovement Movement { get; private set; }

        // ==================== 3. 状态控制 ====================
        
        public bool IsPeaceMode { get; private set; } = false;

        public void Initialize(MaidProfile profile, CharacterMainControl player)
        {
            MainOwner = player; // 缓存主人引用

            // 获取并缓存 AI
            AI = GetComponent<AICharacterController>();
            if (AI == null) AI = GetComponentInChildren<AICharacterController>();
            
            if (AI == null)
            {
                Debug.LogError($"{LogTag} 严重错误：找不到 AICharacterController！");
                return;
            }
            
            // 注册到全局表
            if (!_maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Add(AI, this);
            }

            // 设置原生参数
            AI.leader = player;
            AI.patrolRange = 100.0f; // 似乎无效

            // 初始化子模块
            Movement = GetComponent<MaidMovement>();
            if (Movement == null) Movement = gameObject.AddComponent<MaidMovement>();
            Movement.Initialize(this); 

            Debug.Log($"{LogTag} {profile.Config.CustomName} 初始化完毕。");
        }

        private void OnDestroy()
        {
            if (AI != null && _maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Remove(AI);
            }
        }

        public void SetPeaceMode(bool enable)
        {
            IsPeaceMode = enable;
            if (enable && AI != null)
            {
                AI.searchedEnemy = null;
                AI.aimTarget = null;
                AI.alert = false;
                AI.noticed = false;
            }
        }

        public bool IsOverrideActive => Movement != null && Movement.IsActive;

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