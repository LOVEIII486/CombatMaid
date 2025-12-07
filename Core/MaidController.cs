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
    /// 女仆核心控制器：负责组件组装、状态机驱动及对外接口
    /// </summary>
    public class MaidController : MonoBehaviour
    {
        private static readonly Dictionary<AICharacterController, MaidController> _maidRegistry 
            = new Dictionary<AICharacterController, MaidController>();

        public static MaidController GetMaid(AICharacterController ai)
        {
            if (ai == null) return null;
            return _maidRegistry.TryGetValue(ai, out var maid) ? maid : null;
        }

        #region 核心

        public AICharacterController AI { get; private set; }
        public CharacterMainControl MaidCharacter => AI != null ? AI.CharacterMainControl : null;
        public CharacterMainControl MainOwner { get; private set; }
        
        public MaidStateMachine StateMachine { get; private set; }
        public MaidSkillComponent SkillSystem { get; private set; }

        #endregion

        #region 默认设置

        public float ForceFollowDistance = 15.0f;        // 强制跟随触发距离
        public float HoldMaxDistance = 25.0f;            // 驻守模式最大宽容距离
        public float TeleportDistance = 30.0f;           // 强制传送距离
        public float TeleportTimeout = 5.0f;             // 强制跟随卡死超时时间
        public float SafeDistanceToResumeCombat = 5.0f; // 恢复自主战斗的安全距离

        /// <summary>
        /// 是否处于非原版接管状态
        /// </summary>
        public bool IsOverrideActive => StateMachine != null && !(StateMachine.CurrentState is State_Autonomous);

        #endregion

        #region 基础

        public void Initialize(MaidProfileData profileData, CharacterMainControl player, AICharacterController preCachedAI = null)
        {
            MainOwner = player;
            
            AI = preCachedAI != null ? preCachedAI : GetComponentInChildren<AICharacterController>();
            if (AI == null)
            {
                CMDebug.LogError($"严重错误：找不到 AICharacterController！");
                return;
            }

            // 注册实例
            if (!_maidRegistry.ContainsKey(AI)) _maidRegistry.Add(AI, this);

            // 初始化技能系统
            SkillSystem = gameObject.GetComponent<MaidSkillComponent>() ?? gameObject.AddComponent<MaidSkillComponent>();
            SkillSystem.Initialize(this);
            
            // 固有技能：小队协同
            SkillSystem.AddSkill(new Skill_SquadCoordination());
            
            // 加载配置技能
            if (profileData.ExtraData?.Skills != null)
            {
                foreach (var skillConfig in profileData.ExtraData.Skills)
                {
                    var skill = MaidSkillFactory.CreateSkill(skillConfig);
                    if (skill != null) SkillSystem.AddSkill(skill);
                }
            }
    
            // 初始化状态机
            InitializeStateMachine();
    
            CMDebug.Log($"女仆控制器初始化完成。主人: {player.name}, 技能数: {SkillSystem.SkillCount}");
        }

        private void Update()
        {
            if (AI == null || MaidCharacter == null || MaidCharacter.Health.IsDead) return;
            StateMachine?.Update();
        }

        private void OnDestroy()
        {
            if (AI != null && _maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Remove(AI);
            }
        }

        #endregion

        #region 状态机初始化

        private void InitializeStateMachine()
        {
            StateMachine = new MaidStateMachine(this);

            StateMachine.AddState(new State_Autonomous());
            StateMachine.AddState(new State_TacticalMove());
            StateMachine.AddState(new State_ForceFollow());
            StateMachine.AddState(new State_HoldPosition());
            StateMachine.AddState(new State_PassiveFollow());

            StateMachine.ChangeState<State_Autonomous>();
        }

        #endregion

        #region 对外api

        /// <summary>
        /// 强制移动指令 (G键)
        /// </summary>
        public void ForceMoveTo(Vector3 position)
        {
            // 检查当前是否已经是战术移动状态
            if (StateMachine.CurrentState is State_TacticalMove tacticalState)
            {
                // 1. 更新目标点
                tacticalState.TargetPosition = position;
                // 2. 强制“重入”状态
                // 重新触发 Enter() 里的 AI.MoveToPos 逻辑
                // 从而打断当前的卡死状态，执行新的移动
                tacticalState.Enter(); 
                CMDebug.Log($"[MaidController] 刷新战术移动目标 -> {position}");
                return;
            }

            // 如果是其他状态，走正常流程切换
            StateMachine.ChangeState<State_TacticalMove>(state => 
            {
                state.TargetPosition = position;
            });
        }
        
        /// <summary>
        /// 强制治疗指令 (H键)
        /// </summary>
        public void ForceHeal()
        {
            if (SkillSystem == null) return;

            var healSkill = SkillSystem.GetSkill<Skill_SelfHeal>();
            if (healSkill != null)
            {
                // CMDebug.Log($"{MaidCharacter.name} 收到强制治疗指令...");
                healSkill.ForceActivate(); 
            }
        }

        /// <summary>
        /// 检查是否距离主人过远
        /// </summary>
        public bool IsTooFarFromOwner()
        {
            if (MainOwner == null) return false;
            
            float dist = Vector3.Distance(transform.position, MainOwner.transform.position);
            
            // 紧急情况传送判断交给状态机，此处仅返回距离阈值
            if (dist > TeleportDistance) return true;
            return dist > ForceFollowDistance;
        }

        #endregion
    }
}