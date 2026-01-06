using UnityEngine;
using CombatMaid.Core.AttributeModifiers;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 强制跟随状态
    /// </summary>
    public class State_ForceFollow : MaidStateBase
    {
        private float _stuckTimer;
        
        // --- 赶路参数配置 ---
        private const float SpeedMultiplier = 2.5f;   // 250% 移速
        private const float AccMultiplier = 8.0f;     // 800% 加速度

        /// <summary>
        /// 归队后该切回哪个状态的逻辑回调
        /// </summary>
        public System.Action NextStateAction; 

        public override void Enter()
        {
            SetNativeBrainActive(true);
            Controller.SetSensorySuppression(true);

            if (Controller.AI != null)
            {
                Controller.AI.leader = Controller.MainOwner;
            }
            
            // 如果没有指定返回状态，默认回自主模式
            if (NextStateAction == null)
            {
                NextStateAction = () => Machine.ChangeState<State_Autonomous>();
            }
            
            ApplySpeedBuffs();
            
            Controller.MaidCharacter?.PopText("<color=#FFD700>主人等等我！！！</color>");
            _stuckTimer = 0f;
        }

        private void ApplySpeedBuffs()
        {
            var character = Controller.MaidCharacter;
            if (character == null) return;

            AttributeModifier.Modify(character, StatModifier.Attributes.WalkSpeed, SpeedMultiplier, true, this);
            AttributeModifier.Modify(character, StatModifier.Attributes.RunSpeed, SpeedMultiplier, true, this);
            
            AttributeModifier.Modify(character, StatModifier.Attributes.WalkAcc, AccMultiplier, true, this);
            AttributeModifier.Modify(character, StatModifier.Attributes.RunAcc, AccMultiplier, true, this);
        }

        public override void Update()
        {
            if (Controller.MainOwner == null || Controller.AI == null) return;

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            
            // 赶路期间忽略一切威胁，强制清理 AI 的敌对目标
            if (Controller.AI.searchedEnemy != null || Controller.AI.noticed)
            {
                Controller.ClearImmediateThreats();
            }
            
            if (Controller.AI.leader != Controller.MainOwner)
            {
                Controller.AI.leader = Controller.MainOwner;
            }

            _stuckTimer += Time.deltaTime;
            
            // 超远距离或长时间卡死则强制传送
            if (dist > Controller.TeleportDistance || _stuckTimer > Controller.TeleportTimeout)
            {
                Teleport();
                NextStateAction?.Invoke();
                return;
            }

            // 到达安全范围内，任务完成
            if (dist < Controller.SafeDistanceToResumeCombat)
            {
                Controller.MaidCharacter?.PopText("回来啦~");
                NextStateAction?.Invoke();
                return;
            }
        }

        public override void Exit()
        {
            Controller.SetSensorySuppression(false);

            if (Controller.MaidCharacter != null)
            {
                AttributeModifier.ClearAll(Controller.MaidCharacter, this);
            }
            
            if (Controller.AI != null)
            {
                Controller.AI.StopMove(); // 状态切换时先停下，防止位移惯性
            }
            
            NextStateAction = null;
        }

        private void Teleport()
        {
            Controller.transform.position = Controller.MainOwner.transform.position;
            
            // 同步 AI 控制器的位置
            if (Controller.AI != null && Controller.AI.transform.parent != Controller.transform)
            {
                Controller.AI.transform.position = Controller.MainOwner.transform.position;
            }
            
            Controller.MaidCharacter?.PopText("<color=red>强制传送</color>");
            if (Controller.AI != null) Controller.AI.StopMove();
        }
    }
}