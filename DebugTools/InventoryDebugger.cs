using UnityEngine;
using Duckov;
using Duckov.UI;
using HarmonyLib; 
using System.Reflection;
using CombatMaid.Core;
using ItemStatsSystem; // 引用核心命名空间以访问 MaidManager

namespace CombatMaid.DebugTools
{
    public class InventoryDebugger : MonoBehaviour
    {
        // === 配置部分 ===
        private const KeyCode WineFoxKey = KeyCode.F2; // [新] 直接打开酒狐背包
        private const float RayDistance = 50f;
        
        // 反射缓存
        private static FieldInfo _lootTargetDisplayField;
        private static FieldInfo _lootTargetInventoryField;
        private static FieldInfo _lootTargetNameField;
        
        private static FieldInfo _characterItemControlField;
        private static PropertyInfo _controlInventoryProp;
        private static FieldInfo _controlInventoryField;

        private void Awake()
        {
            InitReflection();
            CMDebug.LogInfo($"背包调试器已就绪:\n[F2] 查看准心目标\n[F3] 直接打开酒狐背包 (无需瞄准)");
        }

        private void Update()
        {

            // 新功能：直接获取酒狐
            if (Input.GetKeyDown(WineFoxKey))
            {
                OpenWineFoxDirectly();
            }
        }

        /// <summary>
        /// [新功能] 直接从管理器获取酒狐引用
        /// </summary>
        private void OpenWineFoxDirectly()
        {
            if (MaidManager.Instance == null)
            {
                CMDebug.LogError("MaidManager 未初始化！");
                return;
            }

            // 1. 获取酒狐控制器
            var wineFoxController = MaidManager.Instance.GetActiveWineFox();
            if (wineFoxController == null)
            {
                CMDebug.LogWarning("当前没有活跃的酒狐单位！(请先按F5生成)");
                return;
            }

            // 2. 获取 CharacterMainControl
            var targetChar = wineFoxController.MaidCharacter;
            if (targetChar == null)
            {
                CMDebug.LogError("酒狐存在，但其 CharacterMainControl 为空！");
                return;
            }

            CMDebug.LogInfo($">>> 快速访问: {targetChar.name} <<<");
            OpenUIForCharacter(targetChar);
        }

        // 通用的打开UI逻辑
        private void OpenUIForCharacter(CharacterMainControl character)
        {
            if (LootView.Instance == null)
            {
                CMDebug.LogError("LootView UI实例不存在。");
                return;
            }

            try
            {
                // 获取背包数据
                Inventory targetInventory = GetInventoryFromCharacter(character);
                if (targetInventory == null) return;

                var lootView = LootView.Instance;
                
                // 1. 设置 LootView 内部字段
                _lootTargetInventoryField.SetValue(lootView, targetInventory);

                // 2. 设置标题
                var nameText = _lootTargetNameField.GetValue(lootView) as TMPro.TextMeshProUGUI;
                if (nameText != null) 
                {
                    // 如果是酒狐，显示特殊名字
                    bool isWineFox = character.name.Contains("WineFox") || character.name.Contains("酒狐");
                    nameText.text = isWineFox ? $"[契约] 酒狐的背包" : $"[搜刮] {character.name}";
                }

                // 3. 初始化显示组件
                var targetDisplay = _lootTargetDisplayField.GetValue(lootView) as InventoryDisplay;
                if (targetDisplay != null)
                {
                    // 参数: target, highlightFunc, operateFunc, movable, filter
                    // movable = true 允许拖拽
                    targetDisplay.Setup(targetInventory, null, null, true, null);
                }

                // 4. 显示
                lootView.Show();
                CMDebug.LogInfo("背包UI已打开。");
            }
            catch (System.Exception e)
            {
                CMDebug.LogError($"UI打开失败: {e.Message}");
            }
        }

        private Inventory GetInventoryFromCharacter(CharacterMainControl character)
        {
            if (_characterItemControlField == null) return null;

            var itemControl = _characterItemControlField.GetValue(character);
            if (itemControl == null) return null;

            // 优先尝试属性
            if (_controlInventoryProp != null)
            {
                return _controlInventoryProp.GetValue(itemControl) as Inventory;
            }
            // 备用尝试字段
            if (_controlInventoryField != null)
            {
                return _controlInventoryField.GetValue(itemControl) as Inventory;
            }

            CMDebug.LogError("无法反射获取 Inventory。");
            return null;
        }

        private void InitReflection()
        {
            // LootView 字段
            _lootTargetDisplayField = AccessTools.Field(typeof(LootView), "lootTargetInventoryDisplay");
            _lootTargetInventoryField = AccessTools.Field(typeof(LootView), "targetInventory");
            _lootTargetNameField = AccessTools.Field(typeof(LootView), "lootTargetDisplayName");

            // Character 字段
            _characterItemControlField = AccessTools.Field(typeof(CharacterMainControl), "itemControl");
            _controlInventoryProp = AccessTools.Property(typeof(CharacterItemControl), "inventory"); 
            _controlInventoryField = AccessTools.Field(typeof(CharacterItemControl), "inventory");
        }
    }
}