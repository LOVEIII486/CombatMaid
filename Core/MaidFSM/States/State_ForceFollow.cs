using UnityEngine;
using System.Collections.Generic;
using CombatMaid.Core.AttributeModifiers;
using ItemStatsSystem.Stats;

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 强制跟随模式：防卡死、传送
    /// </summary>
    public class State_ForceFollow : MaidStateBase
    {
        private float _checkTimer;
        private float _stuckTimer;
        
        // 缓存临时的速度修改器
        private List<Modifier> _speedBuffs = new List<Modifier>();
        // 强制跟随时的速度倍率
        private const float SpeedMultiplier = 3f;

        public override void Enter()
        {
            // 1. 暂停 AI
            SetNativeBrainActive(false);
            
            // 2. 清除战斗目标
            if (Controller.AI != null)
            {
                Controller.AI.searchedEnemy = null;
                Controller.AI.aimTarget = null;
                Controller.AI.StopMove();
            }
            
            // 3. 添加临时移速加成
            AttributeModifier.Quick.RevertSpeedModifiers(Controller.MaidCharacter, _speedBuffs);
            _speedBuffs = AttributeModifier.Quick.ModifySpeed(Controller.MaidCharacter, SpeedMultiplier);
            
            Controller.MaidCharacter?.PopText("主人等等我！！！");
            _stuckTimer = 0f;
            
            // 立即触发一次移动
            MoveToOwner();
        }

        public override void Update()
        {
            if (Controller.MainOwner == null) return;

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            
            // 1. 检查是否需要传送
            if (dist > Controller.TeleportDistance || _stuckTimer > Controller.TeleportTimeout)
            {
                Teleport();
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            // 2. 检查是否已经回到了安全距离
            if (dist < Controller.SafeDistanceToResumeCombat)
            {
                Controller.MaidCharacter?.PopText("回来啦~");
                Machine.ChangeState<State_Autonomous>();
                return;
            }

            // 3. 移动
            _checkTimer += Time.deltaTime;
            _stuckTimer += Time.deltaTime;
            
            if (_checkTimer > 1.0f)
            {
                MoveToOwner();
                _checkTimer = 0f;
            }
        }

        // 状态退出时清理 Buff
        public override void Exit()
        {
            AttributeModifier.Quick.RevertSpeedModifiers(Controller.MaidCharacter, _speedBuffs);
            
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
            }
        }

        private void MoveToOwner()
        {
            if (Controller.AI != null)
            {
                Controller.AI.MoveToPos(Controller.MainOwner.transform.position);
            }
        }

        private void Teleport()
        {
            Controller.transform.position = Controller.MainOwner.transform.position;
            if (Controller.AI != null && Controller.AI.transform.parent != Controller.transform)
            {
                Controller.AI.transform.position = Controller.MainOwner.transform.position;
            }
            Controller.MaidCharacter?.PopText("强制传送");
        }
    }
}