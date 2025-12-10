using UnityEngine;
using Duckov;
using Duckov.UI;
using HarmonyLib; 
using System.Reflection;
using CombatMaid.Core;
using ItemStatsSystem;

namespace CombatMaid.DebugTools
{
    public class InventoryDebugger : MonoBehaviour
    {
        // === 配置部分 ===
        private const KeyCode RaycastKey = KeyCode.F2; // 射线查看
        private const KeyCode ExchangeKey = KeyCode.F3; // [新] 玩家与女仆交互模式
        private const float RayDistance = 50f;
        
        // 反射缓存
        private static FieldInfo _lootTargetDisplayField;     // 左侧显示组件
        private static FieldInfo _lootTargetInventoryField;   // 左侧数据引用
        private static FieldInfo _lootTargetNameField;        // 左侧标题
        
        private static FieldInfo _rightSlotDisplayField;      // 右侧装备组件
        private static FieldInfo _rightInventoryDisplayField; // 右侧背包组件

        private static FieldInfo _characterItemControlField;
        private static PropertyInfo _controlInventoryProp;
        private static FieldInfo _controlInventoryField;

        private void Awake()
        {
            InitReflection();
            CMDebug.LogInfo($"[交互面板就绪]\nF2: 查看目标背包\nF3: 打开【物资交换】(左:你 <-> 右:女仆)");
        }

        private void Update()
        {
            if (Input.GetKeyDown(RaycastKey)) TryOpenRaycastTarget();
            if (Input.GetKeyDown(ExchangeKey)) OpenPlayerMaidExchange();
        }

        /// <summary>
        /// [F3] 打开交换面板
        /// 左侧：玩家背包
        /// 右侧：女仆装备 + 女仆背包
        /// </summary>
        private void OpenPlayerMaidExchange()
        {
            if (MaidManager.Instance == null) return;
            var wineFox = MaidManager.Instance.GetActiveWineFox();
            
            if (wineFox == null || wineFox.MaidCharacter == null)
            {
                CMDebug.LogWarning("未找到活跃的酒狐单位！");
                return;
            }

            var player = CharacterMainControl.Main;
            if (player == null) return;

            CMDebug.LogInfo($">>> 开启交换模式: {player.name} <-> {wineFox.name} <<<");
            
            // 右侧显示女仆 (全功能)，左侧显示玩家 (仅背包)
            OpenDualModeUI(mainSide: wineFox.MaidCharacter, otherSide: player);
        }

        /// <summary>
        /// 双向交互UI核心逻辑
        /// </summary>
        /// <param name="mainSide">显示在右侧的角色（拥有装备栏权限）</param>
        /// <param name="otherSide">显示在左侧的角色（作为容器）</param>
        private void OpenDualModeUI(CharacterMainControl mainSide, CharacterMainControl otherSide)
        {
            if (LootView.Instance == null) return;

            try
            {
                var lootView = LootView.Instance;
                
                // === 准备数据 ===
                // 1. 右侧（女仆）数据
                var mainInventory = GetInventoryFromCharacter(mainSide);
                var mainItem = mainSide.CharacterItem; // 包含装备槽

                // 2. 左侧（玩家）数据
                var otherInventory = GetInventoryFromCharacter(otherSide);

                if (mainInventory == null || otherInventory == null)
                {
                    CMDebug.LogError("无法获取双方背包数据。");
                    return;
                }

                // === 打开并劫持 UI ===
                
                // 1. 先调用 Show()，让 LootView 完成它自己的初始化（默认右侧是玩家）
                lootView.Show();

                // 2. [劫持右侧] -> 设为女仆
                // 右侧装备栏
                var rightSlotDisplay = _rightSlotDisplayField.GetValue(lootView) as ItemSlotCollectionDisplay;
                if (rightSlotDisplay != null)
                {
                    // 参数: Item target, bool movable
                    // movable=true 让你可以脱下她的装备
                    rightSlotDisplay.Setup(mainItem, true);
                }

                // 右侧背包
                var rightInvDisplay = _rightInventoryDisplayField.GetValue(lootView) as InventoryDisplay;
                if (rightInvDisplay != null)
                {
                    // 设置为女仆背包
                    rightInvDisplay.Setup(mainInventory, null, null, true, null);
                }

                // 3. [劫持左侧] -> 设为玩家
                // 设置 LootView 内部的 targetInventory，这样“全部拿取”等按钮会作用于左侧
                _lootTargetInventoryField.SetValue(lootView, otherInventory);

                var leftInvDisplay = _lootTargetDisplayField.GetValue(lootView) as InventoryDisplay;
                if (leftInvDisplay != null)
                {
                    // 设置为玩家背包
                    // 注意：这里我们把玩家背包当做“箱子”打开
                    leftInvDisplay.Setup(otherInventory, null, null, true, null);
                }

                // 4. 更新标题
                var nameText = _lootTargetNameField.GetValue(lootView) as TMPro.TextMeshProUGUI;
                if (nameText != null)
                {
                    nameText.text = $"[交换] 左: {otherSide.name} (你)   <--->   右: {mainSide.name} (女仆)";
                }

                CMDebug.LogInfo("交互面板已构建完成。");
            }
            catch (System.Exception e)
            {
                CMDebug.LogError($"UI劫持失败: {e}");
            }
        }

        private void TryOpenRaycastTarget()
        {
            // F2 保持简单的单向查看，或者也可以用这个 DualMode
            var cam = Camera.main;
            if (cam == null) return;
            if (Physics.Raycast(new Ray(cam.transform.position, cam.transform.forward), out RaycastHit hit, RayDistance))
            {
                var target = hit.collider.GetComponentInParent<CharacterMainControl>();
                if (target != null && target != CharacterMainControl.Main)
                {
                    // 同样使用双向交互模式：右侧是目标，左侧是你自己
                    OpenDualModeUI(mainSide: target, otherSide: CharacterMainControl.Main);
                }
            }
        }

        private Inventory GetInventoryFromCharacter(CharacterMainControl character)
        {
            if (_characterItemControlField == null) return null;
            var itemControl = _characterItemControlField.GetValue(character);
            if (itemControl == null) return null;

            if (_controlInventoryProp != null) return _controlInventoryProp.GetValue(itemControl) as Inventory;
            if (_controlInventoryField != null) return _controlInventoryField.GetValue(itemControl) as Inventory;
            return null;
        }

        private void InitReflection()
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
        }
    }
}