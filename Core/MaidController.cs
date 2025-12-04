using System.Collections.Generic;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidFSM;
using CombatMaid.Core.MaidFSM.States;
using CombatMaid.Core.MaidSkillSystem;
using CombatMaid.Core.MaidSkillSystem.Skills;

namespace CombatMaid.Core
{
    /// <summary>
    /// 女仆核心控制器
    /// 职责：组件组装、状态机驱动、对外接口
    /// </summary>
    public class MaidController : MonoBehaviour
    {
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
        /// 有限状态机：管理 AI 的行为模式
        /// </summary>
        public MaidStateMachine StateMachine { get; private set; }
        
        public MaidSkillComponent SkillSystem { get; private set; }
        

        // ==================== 配置参数 ====================
        
        [Header("Distance Config")]
        public float ForceFollowDistance = 15.0f;     // 超过此距离 -> 请求进入强制跟随状态
        public float HoldMaxDistance = 25.0f;         // 驻守模式下的最大宽容距离 (超过这个距离才会破防跟上)
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
                CMDebug.LogError($"严重错误：找不到 AICharacterController！");
                return;
            }

            // 2. 注册到全局字典
            if (!_maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Add(AI, this);
            }

            // 3. 设置基础 AI 归属
            AI.leader = player;
            AI.patrolRange = 25.0f; // 给予较大的巡逻范围，具体由状态机控制
            AI.patrolPosition = player.transform.position;

            // 4. 初始化技能系统
            SkillSystem = gameObject.GetComponent<MaidSkillComponent>();
            if (SkillSystem == null) SkillSystem = gameObject.AddComponent<MaidSkillComponent>();
            SkillSystem.Initialize(this);
            
            SkillSystem.AddSkill(new Skill_SquadCoordination());
            
            if (profileData.ExtraData != null)
            {
                var extra = profileData.ExtraData;

                // 1. 自动回血
                if (extra.EnableAutoHeal)
                {
                    SkillSystem.AddSkill(new Skill_SelfHeal());
                }

                // 2. 投掷手雷
                if (extra.EnableGrenade)
                {
                    int grenId = extra.GrenadeItemID > 0 ? extra.GrenadeItemID : 67;
                    SkillSystem.AddSkill(new Skill_GrenadeThrower(grenId));
                }

                // 3. 施加 自定义 Buff
                if (!string.IsNullOrEmpty(extra.BuffSkillName) && extra.BuffSkillID > 0)
                {
                    SkillSystem.AddSkill(new Skill_BuffPlayer(extra.BuffSkillName, extra.BuffSkillID));
                }
            }
    
            // 5. 初始化状态机
            InitializeStateMachine();
    
            CMDebug.Log($"初始化完成。宿主: {player.name}, 技能数: {SkillSystem.SkillCount}");
        }

        private void InitializeStateMachine()
        {
            StateMachine = new MaidStateMachine(this);

            // 注册所有可用状态
            StateMachine.AddState(new State_Autonomous());
            StateMachine.AddState(new State_TacticalMove());
            StateMachine.AddState(new State_ForceFollow());
            StateMachine.AddState(new State_HoldPosition());
            StateMachine.AddState(new State_PassiveFollow());

            // 启动默认状态 自主模式
            StateMachine.ChangeState<State_Autonomous>();
        }

        private void Update()
        {
            if (AI == null || MaidCharacter == null || MaidCharacter.Health.IsDead) 
                return;

            // 驱动状态机心跳
            StateMachine?.Update();
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
            
            // 紧急情况：如果距离极远，直接在这里处理传送，
            // 但为了逻辑统一，建议尽量交给 State_ForceFollow 处理。
            // 这里只做阈值判断。
            if (dist > TeleportDistance) return true;
            
            return dist > ForceFollowDistance;
        }
    }
}