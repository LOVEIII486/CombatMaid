using UnityEngine;
using Duckov;
using Duckov.UI;
using HarmonyLib; 
using System.Reflection;
using ItemStatsSystem;
using CombatMaid.Core; // 引用核心

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

            CMDebug.Log($"打开物资交换面板: {player.name} <-> {maidCharacter.name}");
            
            // 初始化反射 (如果尚未初始化)
            EnsureReflectionInitialized();

            // 执行UI劫持逻辑
            OpenDualModeUI(mainSide: maidCharacter, otherSide: player);
        }

        /// <summary>
        /// 双向交互UI核心逻辑
        /// </summary>
        /// <param name="mainSide">显示在右侧的角色（拥有装备栏权限，即女仆）</param>
        /// <param name="otherSide">显示在左侧的角色（作为容器，即玩家）</param>
        private static void OpenDualModeUI(CharacterMainControl mainSide, CharacterMainControl otherSide)
        {
            if (LootView.Instance == null)
            {
                CMDebug.LogError("LootView 实例不存在，无法打开界面。");
                return;
            }

            try
            {
                var lootView = LootView.Instance;
                
                // === 1. 准备数据 ===
                var mainInventory = GetInventoryFromCharacter(mainSide);
                var mainItem = mainSide.CharacterItem; // 包含装备槽

                var otherInventory = GetInventoryFromCharacter(otherSide);

                if (mainInventory == null || otherInventory == null)
                {
                    CMDebug.LogError("无法获取背包数据，操作取消。");
                    return;
                }

                // === 2. 打开并劫持 UI ===
                
                // 先调用 Show()，让 LootView 完成它自己的初始化（默认右侧是玩家）
                lootView.Show();

                // [劫持右侧] -> 设为女仆 (拥有装备栏)
                var rightSlotDisplay = _rightSlotDisplayField.GetValue(lootView) as ItemSlotCollectionDisplay;
                if (rightSlotDisplay != null)
                {
                    // movable=true 允许脱下装备
                    rightSlotDisplay.Setup(mainItem, true);
                }

                var rightInvDisplay = _rightInventoryDisplayField.GetValue(lootView) as InventoryDisplay;
                if (rightInvDisplay != null)
                {
                    rightInvDisplay.Setup(mainInventory, null, null, true, null);
                }

                // [劫持左侧] -> 设为玩家 (作为外部容器)
                // 关键：设置 LootView 内部的 targetInventory，确保“全部拿取”等按钮逻辑作用于左侧
                _lootTargetInventoryField.SetValue(lootView, otherInventory);

                var leftInvDisplay = _lootTargetDisplayField.GetValue(lootView) as InventoryDisplay;
                if (leftInvDisplay != null)
                {
                    leftInvDisplay.Setup(otherInventory, null, null, true, null);
                }

                // [更新标题]
                var nameText = _lootTargetNameField.GetValue(lootView) as TMPro.TextMeshProUGUI;
                if (nameText != null)
                {
                    // 使用本地化或直接显示名字
                    nameText.text = $"{otherSide.name} (仓库)  <--->  {mainSide.name} (装备)";
                }
            }
            catch (System.Exception e)
            {
                CMDebug.LogError($"UI劫持失败: {e}");
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
    }
}