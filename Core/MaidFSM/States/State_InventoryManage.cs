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
            // 1. 冻结 AI 和 移动
            SetNativeBrainActive(false);
            HaltMovement();
            
            // 2. 面向玩家 (提升交互感)
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
            // 使用之前封装好的静态工具类
            MaidInventoryUI.OpenManagementPanel(Controller.MaidCharacter);
            _uiOpened = true;
            
            Controller.MaidCharacter?.PopText("整理装备中...");
            CMDebug.Log($"[{Controller.name}] 进入装备管理状态");
        }

        public override void Update()
        {
            // 持续强制停止移动 (防止物理碰撞推挤导致位移)
            HaltMovement();

            // 4. 监测 UI 关闭事件
            // 如果 LootView 被关闭 (玩家按了 ESC 或点击了关闭)，自动切回自主模式
            if (_uiOpened && !IsLootViewActive())
            {
                Controller.MaidCharacter?.PopText("整理完毕");
                Machine.ChangeState<State_Autonomous>();
            }
        }

        public override void Exit()
        {
            // 退出状态时，如果 UI 还没关 (例如通过代码强行切状态)，则强制关闭 UI
            if (IsLootViewActive())
            {
                // 注意：LootView.Hide() 是静态方法还是实例方法取决于游戏实现
                // 根据类结构 LootView.Hide() 是 public static void Hide() 
                // 但 LootView.out.txt 显示它是 InventoryView 的方法，LootView 继承自 View
                // View 通常有 Hide()。保险起见，我们检查 Instance
                if (LootView.Instance != null) LootView.Instance.Close();
            }
            
            // 恢复 AI 在 State_Autonomous.Enter 中会自动处理，这里不需要显式恢复
            CMDebug.Log($"[{Controller.name}] 退出装备管理状态");
        }

        // --- 辅助方法 ---

        private void HaltMovement()
        {
            if (Controller.AI != null)
            {
                Controller.AI.StopMove();
                Controller.AI.aimTarget = null;
                // 如果有 NavMeshAgent，也可以设置为 isStopped = true
            }
        }

        private bool IsLootViewActive()
        {
            // 检查 LootView 是否处于激活状态
            // 方式 A: 检查 GameplayUIManager 的 ActiveView
            if (GameplayUIManager.Instance != null && GameplayUIManager.Instance.ActiveView is LootView)
                return true;
            
            // 方式 B: 直接检查 LootView 单例的 GameObject
            if (LootView.Instance != null && LootView.Instance.gameObject.activeSelf)
                return true;

            return false;
        }
    }
}