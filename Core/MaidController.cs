using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidBehaviors;
using CombatMaid.Core.MaidFSM;
using CombatMaid.Core.MaidFSM.States;

namespace CombatMaid.Core
{
    /// <summary>
    /// 女仆核心控制器 (FSM重构版)
    /// 职责：组件组装、状态机驱动、对外接口
    /// </summary>
    public class MaidController : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidController]";
        
        // ==================== 静态注册表 ====================
        
        private static readonly Dictionary<AICharacterController, MaidController> _maidRegistry 
            = new Dictionary<AICharacterController, MaidController>();

        public static MaidController GetMaid(AICharacterController ai)
        {
            if (ai == null) return null;
            return _maidRegistry.TryGetValue(ai, out var maid) ? maid : null;
        }
        
        // ==================== 核心引用 ====================
        
        public AICharacterController AI { get; private set; }
        public CharacterMainControl MaidCharacter => AI != null ? AI.CharacterMainControl : null;
        public CharacterMainControl MainOwner { get; private set; }
        
        /// <summary>
        /// 有限状态机：管理 AI 的行为模式 (自主/指令/跟随)
        /// </summary>
        public MaidStateMachine StateMachine { get; private set; }

        // ==================== 功能模块 ====================
        
        // 补血模块 (被动逻辑，独立于状态机)
        public MaidHeal HealBehavior { get; private set; }

        // ==================== 配置参数 ====================
        
        [Header("Distance Config")]
        public float ForceFollowDistance = 15.0f;     // 超过此距离 -> 请求进入强制跟随状态
        public float HoldMaxDistance = 25.0f;         // [新增] 驻守模式下的最大宽容距离 (超过这个距离才会破防跟上)
        public float TeleportDistance = 30.0f;        // 超过此距离 -> 强制传送
        public float TeleportTimeout = 8.0f;          // 强制跟随卡住超过此时间 -> 传送
        public float SafeDistanceToResumeCombat = 10.0f; // 回到此距离内 -> 恢复自主战斗状态

        // ==================== 状态属性 ====================

        /// <summary>
        /// 是否处于非原版接管状态 (用于 HarmonyPatch 拦截原版 AI)
        /// 如果当前状态不是 State_Autonomous，则认为正在 Override
        /// </summary>
        public bool IsOverrideActive => StateMachine != null && !(StateMachine.CurrentState is State_Autonomous);

        // ==================== 初始化与生命周期 ====================

        public void Initialize(MaidProfileData profileData, CharacterMainControl player)
        {
            MainOwner = player;

            // 1. 获取核心组件
            AI = GetComponent<AICharacterController>();
            if (AI == null) AI = GetComponentInChildren<AICharacterController>();
            
            if (AI == null)
            {
                Debug.LogError($"{LogTag} 严重错误：找不到 AICharacterController！");
                return;
            }

            // 2. 注册到全局字典
            if (!_maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Add(AI, this);
            }

            // 3. 设置基础 AI 归属
            AI.leader = player;
            AI.patrolRange = 100.0f; // 给予较大的巡逻范围，具体由状态机控制 patrolPosition
            AI.patrolPosition = player.transform.position;

            // 4. 初始化被动功能模块 (MaidHeal)
            // 只要配置允许，补血模块始终运行，不随状态切换而停止
            if (profileData.ExtraData != null && profileData.ExtraData.EnableAutoHeal)
            {
                HealBehavior = GetComponent<MaidHeal>();
                if (HealBehavior == null) HealBehavior = gameObject.AddComponent<MaidHeal>();
                HealBehavior.Initialize(AI);
            }
            
            // 5. 初始化状态机
            InitializeStateMachine();
            
            Debug.Log($"{LogTag} 初始化完成。宿主: {player.name}, 初始状态: Autonomous");
        }

        private void InitializeStateMachine()
        {
            StateMachine = new MaidStateMachine(this);

            // 注册所有可用状态
            StateMachine.AddState(new State_Autonomous());
            StateMachine.AddState(new State_TacticalMove());
            StateMachine.AddState(new State_ForceFollow());
            StateMachine.AddState(new State_HoldPosition());

            // 启动默认状态 (自主模式)
            StateMachine.ChangeState<State_Autonomous>();
        }

        private void Update()
        {
            if (AI == null || MaidCharacter == null || MaidCharacter.Health.IsDead) 
                return;

            // 1. 驱动状态机心跳
            StateMachine?.Update();

            // 2. 驱动被动模块心跳
            if (HealBehavior != null) HealBehavior.OnUpdate();
        }

        private void OnDestroy()
        {
            if (AI != null && _maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Remove(AI);
            }
        }

        // ==================== 对外接口 (API) ====================

        /// <summary>
        /// 强制移动指令 (G键调用)
        /// 切换到 State_TacticalMove 并执行移动
        /// </summary>
        public void ForceMoveTo(Vector3 position)
        {
            // 使用带参数的切换方法，直接将目标点传递给状态
            StateMachine.ChangeState<State_TacticalMove>(state => 
            {
                state.TargetPosition = position;
            });
        }

        /// <summary>
        /// 供 State_Autonomous 轮询使用
        /// 判断是否距离主人太远，需要请求救援(强制跟随)
        /// </summary>
        public bool IsTooFarFromOwner()
        {
            if (MainOwner == null) return false;
            
            float dist = Vector3.Distance(transform.position, MainOwner.transform.position);
            
            // 紧急情况：如果距离极远(可能掉出地图或被卡飞)，直接在这里处理传送，避免状态机逻辑还没跑完人没了
            // 但为了逻辑统一，建议尽量交给 State_ForceFollow 处理。
            // 这里只做阈值判断。
            if (dist > TeleportDistance) return true;
            
            return dist > ForceFollowDistance;
        }
    }
}