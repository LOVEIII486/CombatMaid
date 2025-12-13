using UnityEngine;
using Duckov.UI; // 引用游戏UI命名空间
using CombatMaid.Core.UI; // 引用我们之前写的 MaidInventoryUI

namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 库存管理状态
    /// 功能：停止行动，打开UI，等待玩家操作完成
    /// </summary>
    public class State_InventoryManage : MaidStateBase
    {
        private bool _uiOpened = false;

        public override void Enter()
        {
            // 1. 冻结 AI
            SetNativeBrainActive(false);
            
            // 2. 面向玩家
            if (Controller.MainOwner != null)
            {
                Vector3 direction = (Controller.MainOwner.transform.position - Controller.transform.position).normalized;
                direction.y = 0; // 保持水平旋转
                if (direction != Vector3.zero)
                {
                    Controller.transform.rotation = Quaternion.LookRotation(direction);
                }
            }

            // 3. 打开管理面板
            MaidInventoryUI.OpenManagementPanel(Controller.MaidCharacter);
            _uiOpened = true;
            
            Controller.MaidCharacter?.PopText("整理装备中...");
            CMDebug.Log($"[{Controller.name}] 进入装备管理状态");
        }

        public override void Update()
        {
            if (_uiOpened && !IsLootViewActive())
            {
                Controller.MaidCharacter?.PopText("整理完毕");
                Machine.ChangeState<State_Autonomous>();
            }
        }

        public override void Exit()
        {
            // if (IsLootViewActive())
            // {
            //     if (LootView.Instance != null) LootView.Instance.Close();
            // }
            
            CMDebug.Log($"[{Controller.name}] 退出装备管理状态");
        }
        

        private bool IsLootViewActive()
        {
            if (LootView.Instance != null && LootView.Instance.gameObject.activeSelf)
                return true;

            return false;
        }
    }
}