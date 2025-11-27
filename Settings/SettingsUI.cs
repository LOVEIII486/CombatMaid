using System.Collections.Generic;
using CombatMaid.Localization;
using CombatMaid.ModSettingsApi;
using UnityEngine;

namespace CombatMaid.Settings
{
    public static class SettingsUI
    {
        private const string LogTag = "[CombatMaid.SettingsUI]";
        
        private const string Key_EnableMaidMode = "EnableMaidMode";
        private const string Key_AttackMultiplier = "AttackMultiplier";
        private const string Key_MoveSpeed = "MoveSpeed";

        public static void Register()
        {
            if (!ModSettingAPI.IsInit)
            {
                Debug.LogError($"{LogTag} ModSettingAPI 未初始化");
                return;
            }

            ModSettingAPI.Clear();

            // ==================== 注册设置项 ====================
            
            // 女仆模式开关
            ModSettingAPI.AddToggle(
                Key_EnableMaidMode, 
                LocalizationManager.GetText("Setting_EnableMaidMode"), 
                CombatMaidConfig.EnableMaidMode, 
                (value) => 
                {
                    CombatMaidConfig.EnableMaidMode = value;
                    Debug.Log($"{LogTag} [实时同步] 女仆模式: {value}");
                }
            );

            // 攻击倍率滑块
            ModSettingAPI.AddSlider(
                Key_AttackMultiplier,
                LocalizationManager.GetText("Setting_AttackMultiplier"),
                CombatMaidConfig.AttackMultiplier,
                new Vector2(0.1f, 5.0f),
                (value) =>
                {
                    CombatMaidConfig.AttackMultiplier = value;
                    Debug.Log($"{LogTag} [实时同步] 攻击倍率: {value:F1}");
                },
                1, 5
            );

            // 移动速度滑块
            ModSettingAPI.AddSlider(
                Key_MoveSpeed,
                LocalizationManager.GetText("Setting_MoveSpeed"),
                CombatMaidConfig.MoveSpeed,
                1, 20,
                (value) =>
                {
                    CombatMaidConfig.MoveSpeed = value;
                    Debug.Log($"{LogTag} [实时同步] 移动速度: {value}");
                }
            );

            // ==================== 创建分组 ====================
            
            ModSettingAPI.AddGroup(
                "CombatMaid_MainGroup", // 分组本身的唯一 ID
                LocalizationManager.GetText("Settings_CombatMaid_Group"),
                new List<string> { Key_EnableMaidMode, Key_AttackMultiplier, Key_MoveSpeed },
                0.7f,
                true,
                false
            );
            
            Debug.Log($"{LogTag} 设置菜单已注册");
        }
    }
}