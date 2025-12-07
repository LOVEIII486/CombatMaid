using System.Collections.Generic;
using CombatMaid.Localization;
using CombatMaid.ModSettingsApi;
using UnityEngine;

namespace CombatMaid.Settings
{
    public static class SettingsUI
    {
        public static void Register()
        {
            if (!ModSettingAPI.IsInit) return;
            ModSettingAPI.Clear();

            // ==================== 1. 全局开关 ====================
            
            ModSettingAPI.AddToggle(
                CombatMaidConfig.Key_EnableMaidMode, 
                LocalizationManager.GetText("Setting_EnableMaidMode"), 
                CombatMaidConfig.EnableMaidMode, 
                (value) => CombatMaidConfig.EnableMaidMode = value
            );
            
            ModSettingAPI.AddToggle(
                CombatMaidConfig.Key_DebugMode, 
                "调试模式 (Debug Mode)",
                CombatMaidConfig.DebugMode, 
                (value) => CombatMaidConfig.DebugMode = value
            );

            // ==================== 2. 属性倍率 ====================

            // 血量倍率
            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_HealthMultiplier,
                LocalizationManager.GetText("Setting_HealthMultiplier"),
                CombatMaidConfig.HealthMultiplier,
                new Vector2(0.1f, 10.0f),
                (value) => CombatMaidConfig.HealthMultiplier = value,
                1, 5
            );

            // 攻击倍率
            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_AttackMultiplier,
                LocalizationManager.GetText("Setting_AttackMultiplier"),
                CombatMaidConfig.AttackMultiplier,
                new Vector2(0.1f, 10.0f),
                (value) => CombatMaidConfig.AttackMultiplier = value,
                1, 5
            );

            // 移动速度
            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_MoveSpeedMultiplier,
                LocalizationManager.GetText("Setting_MoveSpeedMultiplier"),
                CombatMaidConfig.MoveSpeedMultiplier,
                new Vector2(0.1f, 10.0f),
                (value) => CombatMaidConfig.MoveSpeedMultiplier = value,
                1, 5
            );

            // ==================== 3. 按键绑定 ====================

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Move,
                LocalizationManager.GetText("Setting_Key_Move"),
                CombatMaidConfig.KeyMove, KeyCode.G, (v) => CombatMaidConfig.KeyMove = v);

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Heal,
                LocalizationManager.GetText("Setting_Key_Heal"),
                CombatMaidConfig.KeyHeal, KeyCode.H, (v) => CombatMaidConfig.KeyHeal = v);

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Hold,
                LocalizationManager.GetText("Setting_Key_Hold"),
                CombatMaidConfig.KeyHold, KeyCode.F, (v) => CombatMaidConfig.KeyHold = v);

            // ==================== 4. 注册分组 ====================
            
            // Group 1: 核心设置
            ModSettingAPI.AddGroup(
                "CombatMaid_MainGroup",
                LocalizationManager.GetText("Settings_CombatMaid_Group"),
                new List<string> { 
                    CombatMaidConfig.Key_EnableMaidMode, 
                    CombatMaidConfig.Key_DebugMode 
                },
                0.7f, true, true
            );

            // Group 2: 属性设置
            ModSettingAPI.AddGroup(
                "CombatMaid_StatsGroup",
                LocalizationManager.GetText("Settings_CombatMaid_Stats"),
                new List<string> { 
                    CombatMaidConfig.Key_HealthMultiplier, 
                    CombatMaidConfig.Key_AttackMultiplier, 
                    CombatMaidConfig.Key_MoveSpeedMultiplier 
                },
                0.7f, false, false
            );

            // Group 3: 按键设置
            ModSettingAPI.AddGroup(
                "CombatMaid_KeysGroup",
                LocalizationManager.GetText("Settings_CombatMaid_Keys"),
                new List<string> { 
                    CombatMaidConfig.Key_Bind_Move, 
                    CombatMaidConfig.Key_Bind_Heal, 
                    CombatMaidConfig.Key_Bind_Hold 
                },
                0.7f, false, false
            );
        }
    }
}