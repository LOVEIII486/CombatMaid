using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic; 
using System.Linq;
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
        
        // Target 区域 (右侧：玩家)
        private static FieldInfo _targetDisplayField;       
        private static FieldInfo _targetInventoryField;     
        private static FieldInfo _targetNameField;          
        
        // Character 区域 (左侧：女仆)
        private static FieldInfo _characterSlotField;       
        private static FieldInfo _characterInventoryField;  

        // 角色数据字段
        private static FieldInfo _characterItemControlField;
        private static PropertyInfo _controlInventoryProp;
        private static FieldInfo _controlInventoryField;

        /// <summary>
        /// 打开女仆装备管理界面
        /// </summary>
        public static void OpenManagementPanel(CharacterMainControl maidCharacter)
        {
            if (maidCharacter == null)
            {
                CMDebug.LogError("尝试打开管理界面，但女仆引用为空。");
                return;
            }

            var player = CharacterMainControl.Main;
            if (player == null) return;

            // 1. 禁用冲突模组
            QuickLootCompatibility.DisableQuickLoot();

            CMDebug.Log($"打开物资交换面板: 女仆[{maidCharacter.name}] <-> 玩家[{player.name}]");
            
            EnsureReflectionInitialized();

            // 执行UI劫持
            OpenDualModeUI(maid: maidCharacter, playerSource: player);
        }

        private static void OpenDualModeUI(CharacterMainControl maid, CharacterMainControl playerSource)
        {
            if (LootView.Instance == null) return;

            try
            {
                var lootView = LootView.Instance;
                
                // === 1. 准备数据 ===
                var maidInventory = GetInventoryFromCharacter(maid);
                var maidItem = maid.CharacterItem;
                var playerInventory = GetInventoryFromCharacter(playerSource);

                if (maidInventory == null || playerInventory == null) return;

                // [步骤 A]: 注入 Target (玩家) 数据
                _targetInventoryField.SetValue(lootView, playerInventory);

                // === 2. 打开界面 ===
                lootView.Show();

                // [步骤 B]: 挂载观察器 (修复 F 键逻辑)
                var oldObserver = lootView.GetComponent<MaidUIObserver>();
                if (oldObserver != null) Object.Destroy(oldObserver);

                var observer = lootView.gameObject.AddComponent<MaidUIObserver>();
                observer.Maid = maid;
                observer.Player = playerSource;

                // [步骤 C]: 覆盖 Character (女仆) 数据
                var charSlotDisplay = _characterSlotField.GetValue(lootView) as ItemSlotCollectionDisplay;
                if (charSlotDisplay != null)
                {
                    charSlotDisplay.Setup(maidItem, true);
                }

                var charInvDisplay = _characterInventoryField.GetValue(lootView) as InventoryDisplay;
                if (charInvDisplay != null)
                {
                    charInvDisplay.Setup(maidInventory, null, null, true, null);
                }

                // [步骤 D]: 修正标题
                var nameText = _targetNameField.GetValue(lootView) as TMPro.TextMeshProUGUI;
                if (nameText != null)
                {
                    nameText.text = $"{maid.characterPreset.DisplayName} (女仆) <---> 玩家";
                }
            }
            catch (System.Exception e)
            {
                CMDebug.LogError($"UI劫持失败: {e}");
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
                
                _targetDisplayField = AccessTools.Field(tLoot, "lootTargetInventoryDisplay");
                _targetInventoryField = AccessTools.Field(tLoot, "targetInventory");
                _targetNameField = AccessTools.Field(tLoot, "lootTargetDisplayName");

                _characterSlotField = AccessTools.Field(tLoot, "characterSlotCollectionDisplay");
                _characterInventoryField = AccessTools.Field(tLoot, "characterInventoryDisplay");

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
        // [内部类] MaidUIObserver
        // 1. 监听 LootView 关闭，恢复 QuickLoot
        // 2. 监听 F 键，手动接管"玩家->女仆"的物品转移
        // =========================================================
        private class MaidUIObserver : MonoBehaviour
        {
            public CharacterMainControl Maid;
            public CharacterMainControl Player;

            public KeyCode QuickLootKey = KeyCode.F; // 默认F键

            private void Update()
            {
                if (Input.GetKeyDown(QuickLootKey))
                {
                    TryManualTransfer();
                }
            }

            private void OnDisable()
            {
                QuickLootCompatibility.RestoreQuickLoot();
                Destroy(this);
            }

            /// <summary>
            /// 尝试手动执行“从玩家 -> 女仆”的转移
            /// </summary>
            private void TryManualTransfer()
            {
                if (Maid == null || Player == null) return;

                var hoveredItem = GetHoveredItem();
                if (hoveredItem == null) return;

                var playerInv = GetInventory(Player);
                var maidInv = GetInventory(Maid);

                // 只有当物品属于 Player (右侧) 时，原版 F 键会失效
                // 我们拦截这种情况，手动转给 Maid (左侧)
                if (IsItemInInventory(hoveredItem, playerInv))
                {
                    bool success = maidInv.AddAndMerge(hoveredItem);

                    if (success)
                    {
                         CMDebug.Log($"[UI] 手动快速转移: {hoveredItem.DisplayName} -> 女仆");
                    }
                    else
                    {
                        Maid.PopText("女仆背包已满!");
                    }
                }
            }

            private Inventory GetInventory(CharacterMainControl c)
            {
                if (c == null || c.CharacterItem == null) return null;
                return c.CharacterItem.Inventory;
            }

            private bool IsItemInInventory(Item item, Inventory inventory)
            {
                if (item == null || inventory == null) return false;
                return inventory.Contains(item);
            }

            private Item GetHoveredItem()
            {
                if (EventSystem.current == null) return null;

                var pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                foreach (var result in results)
                {
                    var entry = result.gameObject.GetComponentInParent<InventoryEntry>();
                    if (entry != null && entry.Item != null) return entry.Item;
                }
                return null;
            }
        }
    }
}