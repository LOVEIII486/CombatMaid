using UnityEngine;
using System.Collections.Generic;
using CombatMaid.Core.AttributeModifiers;
using ItemStatsSystem.Stats;

namespace CombatMaid.Core.MaidFSM.States
{
    public class State_ForceFollow : MaidStateBase
    {
        private float _stuckTimer;
        
        // 缓存临时的速度修改器
        private List<Modifier> _speedBuffs = new List<Modifier>();
        private const float SpeedMultiplier = 2.5f;

        // 【新增】存储“归队后该切回哪个状态”的逻辑
        public System.Action NextStateAction; 

        public override void Enter()
        {
            SetNativeBrainActive(true);
            Controller.SetSensorySuppression(true);

            if (Controller.AI != null)
            {
                Controller.AI.leader = Controller.MainOwner;
                Controller.AI.PutBackWeapon();
            }
            
            // 【新增】保险逻辑：如果没有指定返回状态，默认回自主模式
            // 这样旧的代码调用这个状态也不会报错
            if (NextStateAction == null)
            {
                NextStateAction = () => Machine.ChangeState<State_Autonomous>();
            }
            
            AttributeModifier.Quick.RevertSpeedModifiers(Controller.MaidCharacter, _speedBuffs);
            _speedBuffs = AttributeModifier.Quick.ModifySpeed(Controller.MaidCharacter, SpeedMultiplier);
            
            Controller.MaidCharacter?.PopText("主人等等我！！！");
            _stuckTimer = 0f;
        }

        public override void Update()
        {
            if (Controller.MainOwner == null || Controller.AI == null) return;

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            
            if (Controller.AI.searchedEnemy != null || Controller.AI.noticed)
            {
                Controller.ClearImmediateThreats();
            }
            
            if (Controller.AI.leader != Controller.MainOwner)
            {
                Controller.AI.leader = Controller.MainOwner;
            }

            _stuckTimer += Time.deltaTime;
            
            if (dist > Controller.TeleportDistance || _stuckTimer > Controller.TeleportTimeout)
            {
                Teleport();
                // 【修改】调用存储的下一步逻辑，而不是写死 Autonomous
                NextStateAction?.Invoke();
                return;
            }

            if (dist < Controller.SafeDistanceToResumeCombat)
            {
                Controller.MaidCharacter?.PopText("回来啦~");
                // 【修改】同上
                NextStateAction?.Invoke();
                return;
            }
        }

        public override void Exit()
        {
            Controller.SetSensorySuppression(false);

            AttributeModifier.Quick.RevertSpeedModifiers(Controller.MaidCharacter, _speedBuffs);
            _speedBuffs.Clear();
            
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
            }
            
            // 【新增】清理回调，防止状态复用时污染下一次调用
            NextStateAction = null;
        }

        private void Teleport()
        {
            Controller.transform.position = Controller.MainOwner.transform.position;
            if (Controller.AI != null && Controller.AI.transform.parent != Controller.transform)
            {
                Controller.AI.transform.position = Controller.MainOwner.transform.position;
            }
            Controller.MaidCharacter?.PopText("强制传送");
            if (Controller.AI != null) Controller.AI.StopMove();
        }
    }
}