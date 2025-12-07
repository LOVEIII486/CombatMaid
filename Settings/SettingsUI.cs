using System.Collections.Generic;
using CombatMaid.Localization;
using CombatMaid.ModSettingsApi;
using UnityEngine;

namespace CombatMaid.Settings
{
    public static class SettingsUI
    {
        private const string LogTag = "[CombatMaid.SettingsUI]";
        
        // 引用 Config 中的 Key 常量，避免手写字符串出错
        
        public static void Register()
        {
            if (!ModSettingAPI.IsInit)
            {
                CMDebug.LogError($"{LogTag} ModSettingAPI 未初始化");
                return;
            }

            ModSettingAPI.Clear();

            // ==================== 1. 常规设置 ====================
            
            ModSettingAPI.AddToggle(
                "EnableMaidMode", // 这里直接用字符串或 Config 中的常量均可
                LocalizationManager.GetText("Setting_EnableMaidMode"), 
                CombatMaidConfig.EnableMaidMode, 
                (value) => 
                {
                    CombatMaidConfig.EnableMaidMode = value;
                    CMDebug.Log($"{LogTag} [实时同步] 女仆模式: {value}");
                }
            );
            
            ModSettingAPI.AddSlider(
                "AttackMultiplier",
                LocalizationManager.GetText("Setting_AttackMultiplier"),
                CombatMaidConfig.AttackMultiplier,
                new Vector2(0.1f, 5.0f),
                (value) => CombatMaidConfig.AttackMultiplier = value,
                1, 5
            );

            ModSettingAPI.AddSlider(
                "MoveSpeed",
                LocalizationManager.GetText("Setting_MoveSpeed"),
                CombatMaidConfig.MoveSpeed,
                1, 20,
                (value) => CombatMaidConfig.MoveSpeed = value
            );
            
            ModSettingAPI.AddToggle(
                "DebugMode", 
                "调试模式 (Debug Mode)",
                CombatMaidConfig.DebugMode, 
                (value) => CombatMaidConfig.DebugMode = value
            );

            // ==================== 2. 按键绑定 ====================

            // G: 战术移动
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Move,
                LocalizationManager.GetText("Setting_Key_Move", "指令: 战术移动"),
                CombatMaidConfig.KeyMove,
                KeyCode.G, // 默认值
                (val) => CombatMaidConfig.KeyMove = val
            );

            // H: 强制治疗
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Heal,
                LocalizationManager.GetText("Setting_Key_Heal", "指令: 强制治疗"),
                CombatMaidConfig.KeyHeal,
                KeyCode.H,
                (val) => CombatMaidConfig.KeyHeal = val
            );

            // F: 驻守切换
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Hold,
                LocalizationManager.GetText("Setting_Key_Hold", "指令: 驻守/跟随"),
                CombatMaidConfig.KeyHold,
                KeyCode.F,
                (val) => CombatMaidConfig.KeyHold = val
            );

            // ==================== 3. 创建分组 ====================
            
            // 主分组
            ModSettingAPI.AddGroup(
                "CombatMaid_MainGroup",
                LocalizationManager.GetText("Settings_CombatMaid_Group"),
                new List<string> { "EnableMaidMode", "AttackMultiplier", "MoveSpeed", "DebugMode" },
                0.7f,
                true,
                false
            );

            // 按键分组
            ModSettingAPI.AddGroup(
                "CombatMaid_KeysGroup",
                LocalizationManager.GetText("Settings_CombatMaid_Keys", "按键设置"),
                new List<string> { 
                    CombatMaidConfig.Key_Bind_Move, 
                    CombatMaidConfig.Key_Bind_Heal, 
                    CombatMaidConfig.Key_Bind_Hold 
                },
                0.7f,
                false,
                false
            );
            
            CMDebug.Log($"{LogTag} 设置菜单已注册");
        }
    }
}