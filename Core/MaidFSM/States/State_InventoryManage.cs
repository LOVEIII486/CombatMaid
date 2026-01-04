using System;
using UnityEngine;
using Duckov.UI; 
using CombatMaid.Core.UI; 

namespace CombatMaid.Core.MaidFSM.States
{
    public class State_InventoryManage : MaidStateBase
    {
        // [新增] 用于记录进入背包管理前的状态类型
        public Type PreviousState { get; set; } 
        private bool _uiOpened = false;

        public override void Enter()
        {
            SetNativeBrainActive(false);
            
            if (Controller.MainOwner != null)
            {
                Vector3 direction = (Controller.MainOwner.transform.position - Controller.transform.position).normalized;
                direction.y = 0; 
                if (direction != Vector3.zero)
                {
                    Controller.transform.rotation = Quaternion.LookRotation(direction);
                }
            }

            MaidInventoryUI.OpenManagementPanel(Controller.MaidCharacter);
            _uiOpened = true;
            
            Controller.MaidCharacter?.PopText("整理装备中...");
            CMDebug.Log($"[{Controller.name}] 进入装备管理状态，记录来源: {PreviousState?.Name}");
        }

        public override void Update()
        {
            // 当 UI 关闭后（LootView 变回非激活状态）触发回退
            if (_uiOpened && !IsLootViewActive())
            {
                _uiOpened = false;
                Controller.MaidCharacter?.PopText("整理完毕");
                ExitToPreviousState(); // [修改] 使用动态回退替代硬编码
            }
        }

        /// <summary>
        /// [新增] 根据记录的状态进行智能回滚
        /// </summary>
        public void ExitToPreviousState()
        {
            // 检查来源状态并切换回去，确保和平模式、驻守等状态得以延续
            if (PreviousState == typeof(State_PassiveFollow))
                Machine.ChangeState<State_PassiveFollow>();
            else if (PreviousState == typeof(State_HoldPosition))
                Machine.ChangeState<State_HoldPosition>();
            else if (PreviousState == typeof(State_TacticalMove))
                Machine.ChangeState<State_TacticalMove>();
            else
                Machine.ChangeState<State_Autonomous>(); // 默认兜底
        }

        public override void Exit()
        {
            CMDebug.Log($"[{Controller.name}] 退出装备管理状态");
        }
        
        private bool IsLootViewActive()
        {
            return LootView.Instance != null && LootView.Instance.gameObject.activeSelf;
        }
    }
}