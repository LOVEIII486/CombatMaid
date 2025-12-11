using UnityEngine;
using Duckov;
using Duckov.UI;
using HarmonyLib; 
using System.Reflection;
using ItemStatsSystem;
using CombatMaid.Core;

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
            if (LootView.Instance == null) return;

            try
            {
                var lootView = LootView.Instance;
                
                // === 1. 准备数据 ===
                var mainInventory = GetInventoryFromCharacter(mainSide); // 女仆 (右)
                var mainItem = mainSide.CharacterItem;
                var otherInventory = GetInventoryFromCharacter(otherSide); // 玩家 (左)

                if (mainInventory == null || otherInventory == null) return;

                // ==============================================================
                // [关键修复] 步骤 A: 在 Show() 之前注入左侧数据
                // ==============================================================
                // 这样 OnOpen() 运行时，会认为"有目标容器"，从而自动显示左侧面板 (FadeGroup.Show)
                _lootTargetInventoryField.SetValue(lootView, otherInventory);

                // === 2. 打开界面 ===
                // 此时 OnOpen 执行：
                // - 左侧：自动 Setup(otherInventory) -> 显示玩家背包 (符合预期)
                // - 右侧：自动 Setup(Player) -> 显示玩家装备 (不符预期，稍后覆盖)
                lootView.Show();

                // ==============================================================
                // 步骤 B: 覆盖右侧面板 (Show 之后执行)
                // ==============================================================
                // 强行把右侧改为女仆数据
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

                // ==============================================================
                // 步骤 C: 修正标题 (Show 之后执行)
                // ==============================================================
                // 因为 OnOpen 会重置标题为 Inventory.DisplayName，所以我们要重新覆盖一次
                var nameText = _lootTargetNameField.GetValue(lootView) as TMPro.TextMeshProUGUI;
                if (nameText != null)
                {
                    nameText.text = $"{mainSide.characterPreset.DisplayName} (女仆背包) <---> 玩家背包";
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