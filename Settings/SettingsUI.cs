using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CombatMaid.Localization;
using CombatMaid.ModSettingsApi;
using CombatMaid.Core.WineFox;
using UnityEngine;

namespace CombatMaid.Settings
{
    public static class SettingsUI
    {
        public static void Register()
        {
            if (!ModSettingAPI.IsInit) return;
            ModSettingAPI.Clear();

            // ==================== 1. 属性倍率 ====================

            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_HealthMultiplier,
                LocalizationManager.GetText("Setting_HealthMultiplier"),
                CombatMaidConfig.HealthMultiplier,
                new Vector2(0.1f, 10.0f),
                (value) => CombatMaidConfig.HealthMultiplier = value,
                1, 5
            );

            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_AttackMultiplier,
                LocalizationManager.GetText("Setting_AttackMultiplier"),
                CombatMaidConfig.AttackMultiplier,
                new Vector2(0.1f, 10.0f),
                (value) => CombatMaidConfig.AttackMultiplier = value,
                1, 5
            );

            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_MoveSpeedMultiplier,
                LocalizationManager.GetText("Setting_MoveSpeedMultiplier"),
                CombatMaidConfig.MoveSpeedMultiplier,
                new Vector2(0.1f, 10.0f),
                (value) => CombatMaidConfig.MoveSpeedMultiplier = value,
                1, 5
            );

            // ==================== 2. 按键绑定 ====================

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
                CombatMaidConfig.KeyHold, KeyCode.J, (v) => CombatMaidConfig.KeyHold = v);
            
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Scavenge,
                LocalizationManager.GetText("Setting_Key_Scavenge"),
                CombatMaidConfig.KeyScavenge, KeyCode.L, (v) => CombatMaidConfig.KeyScavenge = v);

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Drop,
                LocalizationManager.GetText("Setting_Key_Drop"),
                CombatMaidConfig.KeyDrop, KeyCode.K, (v) => CombatMaidConfig.KeyDrop = v);
            
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_InvManage,
                LocalizationManager.GetText("Setting_Key_InvManage"),
                CombatMaidConfig.KeyInventoryManage,KeyCode.B,
                (v) => CombatMaidConfig.KeyInventoryManage = v
            );
            
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Passive,
                LocalizationManager.GetText("Settings_Key_Passive"),
                CombatMaidConfig.KeyPassive, 
                KeyCode.N, 
                (v) => CombatMaidConfig.KeyPassive = v
            );
            
            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_LootMinVal,
                LocalizationManager.GetText("Settings_LootMinValue"),
                CombatMaidConfig.LootMinVal,
                0, 500000,
                (value) => 
                {
                    CombatMaidConfig.LootMinVal = value;
                },
                7
            );

            // ==================== 3. 自定义女仆配置 ====================

            ModSettingAPI.AddInput(
                CombatMaidConfig.Key_CustomMaidName,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_CustomMaidName),
                CombatMaidConfig.CustomMaidName,
                20,
                OnCustomMaidNameChanged
            );

            ModSettingAPI.AddInput(
                CombatMaidConfig.Key_CustomMaidModelID,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_CustomMaidModelID),
                CombatMaidConfig.CustomMaidModelID,
                10,
                OnCustomMaidModelIDChanged
            );
            
            ModSettingAPI.AddInput(
                CombatMaidConfig.Key_BuffBlockList,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_BuffBlockList), 
                CombatMaidConfig.BuffBlockListString,
                100,
                (value) => CombatMaidConfig.ParseBuffBlockList(value)
            );
            
            ModSettingAPI.AddButton(
                "OpenSaveFolder",
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_OpenSaveFolder),
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_OpenSaveFolderButton),
                OpenSaveFolderAction
            );

            // ==================== 4. 注册分组 ====================

            ModSettingAPI.AddGroup(
                "CombatMaid_StatsGroup",
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_Group_Stats),
                new List<string>
                {
                    CombatMaidConfig.Key_HealthMultiplier,
                    CombatMaidConfig.Key_AttackMultiplier,
                    CombatMaidConfig.Key_MoveSpeedMultiplier
                },
                0.7f, true, false
            );

            ModSettingAPI.AddGroup(
                "CombatMaid_KeysGroup",
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_Group_Keys),
                new List<string>
                {
                    CombatMaidConfig.Key_Bind_Move,
                    CombatMaidConfig.Key_Bind_Heal,
                    CombatMaidConfig.Key_Bind_Hold,
                    CombatMaidConfig.Key_Bind_Scavenge,
                    CombatMaidConfig.Key_Bind_Drop,
                    CombatMaidConfig.Key_Bind_InvManage,
                    CombatMaidConfig.Key_Bind_Passive
                },
                0.7f, false, false
            );
            
            ModSettingAPI.AddGroup(
                "CombatMaid_AIGroup",
                LocalizationManager.GetText("Settings_Group_AI"),
                new List<string>
                {
                    CombatMaidConfig.Key_LootMinVal
                },
                0.7f, false, false
            );
            
            ModSettingAPI.AddGroup(
                "CombatMaid_CustomGroup",
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_Group_Customize),
                new List<string>
                {
                    CombatMaidConfig.Key_CustomMaidName,
                    CombatMaidConfig.Key_CustomMaidModelID,
                    CombatMaidConfig.Key_BuffBlockList,
                    "OpenSaveFolder"
                },
                0.7f, false, false
            );
        }


        /// <summary>
        /// 女仆名称变化回调
        /// </summary>
        private static void OnCustomMaidNameChanged(string value)
        {
            CombatMaidConfig.CustomMaidName = value;
            CMDebug.Log($"女仆名称输入: {value}");
            CombatMaidConfig.ApplyCustomMaidName(value);
        }

        /// <summary>
        /// 模型ID变化回调
        /// </summary>
        private static void OnCustomMaidModelIDChanged(string value)
        {
            CombatMaidConfig.CustomMaidModelID = value;
            CMDebug.Log($"模型ID输入: {value}");
            CombatMaidConfig.ApplyCustomModelID(value);
        }

        private static void OpenSaveFolderAction()
        {
            try
            {
                string saveDir = WineFoxDataManager.GetSaveDir();

                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                    CMDebug.Log($"创建存档目录: {saveDir}");
                }

                OpenFolder(saveDir);

                CMDebug.Log($"已打开存档目录: {saveDir}");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"打开存档目录失败: {ex.Message}");
            }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer ||
                    Application.platform == RuntimePlatform.WindowsEditor)
                {
                    Process.Start("explorer.exe", path);
                }
                else if (Application.platform == RuntimePlatform.OSXPlayer ||
                         Application.platform == RuntimePlatform.OSXEditor)
                {
                    Process.Start("open", path);
                }
                else if (Application.platform == RuntimePlatform.LinuxPlayer ||
                         Application.platform == RuntimePlatform.LinuxEditor)
                {
                    Process.Start("xdg-open", path);
                }
                else
                {
                    CMDebug.LogWarning($"不支持的平台: {Application.platform}");
                }
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"打开文件夹失败: {ex.Message}");
            }
        }
    }
}