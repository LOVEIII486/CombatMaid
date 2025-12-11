using UnityEngine;
using Duckov;
using Duckov.UI;
using HarmonyLib; 
using System.Reflection;
using ItemStatsSystem;
using CombatMaid.ModCompatibility;

namespace CombatMaid.Core.UI
{
    /// <summary>
    /// 女仆背包界面管理器
    /// 负责劫持原版 LootView 以显示女仆的装备管理界面
    /// </summary>
    public static class MaidInventoryUI
    {
        // === 反射字段缓存 ===
        private static bool _isReflectionInitialized = false;
        
        private static FieldInfo _lootTargetDisplayField;     // 左侧显示组件
        private static FieldInfo _lootTargetInventoryField;   // 左侧数据引用
        private static FieldInfo _lootTargetNameField;        // 左侧标题
        
        private static FieldInfo _rightSlotDisplayField;      // 右侧装备组件
        private static FieldInfo _rightInventoryDisplayField; // 右侧背包组件

        private static FieldInfo _characterItemControlField;
        private static PropertyInfo _controlInventoryProp;
        private static FieldInfo _controlInventoryField;

        /// <summary>
        /// 打开女仆装备管理界面 (交互模式)
        /// 左侧：玩家背包 (作为仓库)
        /// 右侧：女仆装备 + 女仆背包 (作为主角)
        /// </summary>
        /// <param name="maidCharacter">要管理的女仆角色</param>
        public static void OpenManagementPanel(CharacterMainControl maidCharacter)
        {
            if (maidCharacter == null)
            {
                CMDebug.LogError("尝试打开管理界面，但女仆引用为空。");
                return;
            }

            var player = CharacterMainControl.Main;
            if (player == null) return;

            // [新增] 1. 在操作 UI 前，先禁用冲突模组 (QuickLoot)
            // 防止它在 UI 数据切换瞬间读取到错误状态导致死循环/卡死
            QuickLootCompatibility.DisableQuickLoot();

            CMDebug.Log($"打开物资交换面板: {player.name} <-> {maidCharacter.name}");
            
            // 初始化反射 (如果尚未初始化)
            EnsureReflectionInitialized();

            // 执行UI劫持逻辑
            OpenDualModeUI(mainSide: maidCharacter, otherSide: player);
        }

        /// <summary>
        /// 双向交互UI核心逻辑
        /// </summary>
        private static void OpenDualModeUI(CharacterMainControl mainSide, CharacterMainControl otherSide)
        {
            if (LootView.Instance == null) return;

            try
            {
                var lootView = LootView.Instance;
                
                // === 1. 准备数据 ===
                var mainInventory = GetInventoryFromCharacter(mainSide); // 女仆 (右)
                var mainItem = mainSide.CharacterItem;
                var otherInventory = GetInventoryFromCharacter(otherSide); // 玩家 (左)

                if (mainInventory == null || otherInventory == null) return;

                // [关键修复] 步骤 A: 在 Show() 之前注入左侧数据
                _lootTargetInventoryField.SetValue(lootView, otherInventory);

                // === 2. 打开界面 ===
                lootView.Show();

                // [新增] 2. 挂载关闭监听器
                // 当 LootView 关闭时，这个组件会自动恢复 QuickLoot 并销毁自己
                // 确保不影响其他箱子的正常交互
                if (lootView.GetComponent<MaidUICloseObserver>() == null)
                {
                    lootView.gameObject.AddComponent<MaidUICloseObserver>();
                }

                // 步骤 B: 覆盖右侧面板 (Show 之后执行)
                var rightSlotDisplay = _rightSlotDisplayField.GetValue(lootView) as ItemSlotCollectionDisplay;
                if (rightSlotDisplay != null)
                {
                    rightSlotDisplay.Setup(mainItem, true);
                }

                var rightInvDisplay = _rightInventoryDisplayField.GetValue(lootView) as InventoryDisplay;
                if (rightInvDisplay != null)
                {
                    rightInvDisplay.Setup(mainInventory, null, null, true, null);
                }

                // 步骤 C: 修正标题 (Show 之后执行)
                var nameText = _lootTargetNameField.GetValue(lootView) as TMPro.TextMeshProUGUI;
                if (nameText != null)
                {
                    nameText.text = $"{mainSide.characterPreset.DisplayName} (女仆背包) <---> 玩家背包";
                }
            }
            catch (System.Exception e)
            {
                CMDebug.LogError($"UI劫持失败: {e}");
                // 如果出错，尝试恢复，避免卡在禁用状态
                QuickLootCompatibility.RestoreQuickLoot(); 
            }
        }

        private static Inventory GetInventoryFromCharacter(CharacterMainControl character)
        {
            if (_characterItemControlField == null) return null;
            var itemControl = _characterItemControlField.GetValue(character);
            if (itemControl == null) return null;

            if (_controlInventoryProp != null) return _controlInventoryProp.GetValue(itemControl) as Inventory;
            if (_controlInventoryField != null) return _controlInventoryField.GetValue(itemControl) as Inventory;
            return null;
        }

        private static void EnsureReflectionInitialized()
        {
            if (_isReflectionInitialized) return;

            try
            {
                var tLoot = typeof(LootView);
                
                // 左侧字段
                _lootTargetDisplayField = AccessTools.Field(tLoot, "lootTargetInventoryDisplay");
                _lootTargetInventoryField = AccessTools.Field(tLoot, "targetInventory");
                _lootTargetNameField = AccessTools.Field(tLoot, "lootTargetDisplayName");

                // 右侧字段
                _rightSlotDisplayField = AccessTools.Field(tLoot, "characterSlotCollectionDisplay");
                _rightInventoryDisplayField = AccessTools.Field(tLoot, "characterInventoryDisplay");

                // 角色字段
                _characterItemControlField = AccessTools.Field(typeof(CharacterMainControl), "itemControl");
                _controlInventoryProp = AccessTools.Property(typeof(CharacterItemControl), "inventory"); 
                _controlInventoryField = AccessTools.Field(typeof(CharacterItemControl), "inventory");

                _isReflectionInitialized = true;
            }
            catch (System.Exception e)
            {
                CMDebug.LogError($"MaidInventoryUI 反射初始化失败: {e}");
            }
        }

        // =========================================================
        // [新增] 内部类：用于监听 LootView 关闭事件
        // =========================================================
        /// <summary>
        /// 挂载在 LootView 上，当 LootView 关闭(OnDisable)时，恢复外部模组的功能
        /// </summary>
        private class MaidUICloseObserver : MonoBehaviour
        {
            private void OnDisable()
            {
                // 界面关闭，恢复自动拾取功能
                QuickLootCompatibility.RestoreQuickLoot();
                
                // 任务完成，销毁自己，保持 LootView 干净
                Destroy(this);
            }
        }
    }
}