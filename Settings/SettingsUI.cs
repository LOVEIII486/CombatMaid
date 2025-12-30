using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CombatMaid.Core.CustomModel;
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

            #region 属性倍率

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
            
            #endregion

            #region 按键绑定

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Move,
                LocalizationManager.GetText("Setting_Key_Move"),
                CombatMaidConfig.KeyMove, CombatMaidConfig.Default_KeyMove, (v) => CombatMaidConfig.KeyMove = v);

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Heal,
                LocalizationManager.GetText("Setting_Key_Heal"),
                CombatMaidConfig.KeyHeal, CombatMaidConfig.Default_KeyHeal, (v) => CombatMaidConfig.KeyHeal = v);

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Hold,
                LocalizationManager.GetText("Setting_Key_Hold"),
                CombatMaidConfig.KeyHold, CombatMaidConfig.Default_KeyHold, (v) => CombatMaidConfig.KeyHold = v);
            
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Scavenge,
                LocalizationManager.GetText("Setting_Key_Scavenge"),
                CombatMaidConfig.KeyScavenge, CombatMaidConfig.Default_KeyScavenge, (v) => CombatMaidConfig.KeyScavenge = v);

            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Drop,
                LocalizationManager.GetText("Setting_Key_Drop"),
                CombatMaidConfig.KeyDrop, CombatMaidConfig.Default_KeyDrop, (v) => CombatMaidConfig.KeyDrop = v);
            
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_InvManage,
                LocalizationManager.GetText("Setting_Key_InvManage"),
                CombatMaidConfig.KeyInventoryManage,CombatMaidConfig.Default_KeyInventoryManage,
                (v) => CombatMaidConfig.KeyInventoryManage = v
            );
            
            ModSettingAPI.AddKeybinding(
                CombatMaidConfig.Key_Bind_Passive,
                LocalizationManager.GetText("Settings_Key_Passive"),
                CombatMaidConfig.KeyPassive, 
                CombatMaidConfig.Default_KeyPassive, 
                (v) => CombatMaidConfig.KeyPassive = v
            );

            #endregion

            #region 高级AI设置
            
            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_LootMinVal,
                LocalizationManager.GetText("Settings_LootMinValue"),
                CombatMaidConfig.LootMinVal,
                0, 10000,
                (value) => 
                {
                    CombatMaidConfig.LootMinVal = value;
                },
                7
            );
            ModSettingAPI.AddToggle(
                CombatMaidConfig.Key_IgnoreSearched,
                LocalizationManager.GetText("Settings_IgnoreSearched"),
                CombatMaidConfig.IgnoreSearched,
                (value) => 
                {
                    CombatMaidConfig.IgnoreSearched = value;
                }
            );
            ModSettingAPI.AddToggle(
                CombatMaidConfig.Key_EnableElementalGrenades,
                LocalizationManager.GetText("Settings_EnableElementalGrenades"), 
                CombatMaidConfig.EnableElementalGrenades,
                (value) => 
                {
                    CombatMaidConfig.EnableElementalGrenades = value;
                }
            );
            ModSettingAPI.AddToggle(
                CombatMaidConfig.Key_TransferKillToOwner,
                LocalizationManager.GetText("Settings_TransferKillToOwner"), 
                CombatMaidConfig.TransferKillToOwner,
                (value) => 
                {
                    CombatMaidConfig.TransferKillToOwner = value;
                }
            );

            #endregion
            
            #region 自定义女仆配置

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
            
            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_MaidAlertVolume,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_MaidAlertVolume),
                CombatMaidConfig.MaidAlertVolume,
                new Vector2(0f, 1.0f),
                (value) => 
                {
                    CombatMaidConfig.MaidAlertVolume = value;
                },
                2
            );
            
            ModSettingAPI.AddSlider(
                CombatMaidConfig.Key_MaidVoiceVolume,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_MaidVoiceVolume),
                CombatMaidConfig.MaidVoiceVolume,
                new Vector2(0.0f, 1.0f),
                (value) => 
                {
                    CombatMaidConfig.MaidVoiceVolume = value;
                    CustomModelAudioPatcher.GlobalMaidVolume = value;
                },
                2,
                5
            );
            
            ModSettingAPI.AddInput(
                CombatMaidConfig.Key_BuffBlockList,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_BuffBlockList), 
                CombatMaidConfig.BuffBlockListString,
                100,
                CombatMaidConfig.ParseBuffBlockList
            );
            
            ModSettingAPI.AddButton(
                CombatMaidConfig.Key_OpenSaveFolder,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_OpenSaveFolder),
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_OpenSaveFolderButton),
                OpenSaveFolderAction
            );
            
            ModSettingAPI.AddButton(
                CombatMaidConfig.Key_Button_RebuildWineFox,
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_Button_RebuildDesc),
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_Button_RebuildName),
                WineFoxDataManager.RebuildWineFoxSaveData
            );

            #endregion
            
            #region 注册分组

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
                LocalizationManager.GetText(CombatMaidConfig.LocalKey_Group_AI),
                new List<string>
                {
                    CombatMaidConfig.Key_LootMinVal,
                    CombatMaidConfig.Key_IgnoreSearched,
                    CombatMaidConfig.Key_EnableElementalGrenades,
                    CombatMaidConfig.Key_TransferKillToOwner
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
                    CombatMaidConfig.Key_MaidAlertVolume,
                    CombatMaidConfig.Key_MaidVoiceVolume,
                    CombatMaidConfig.Key_BuffBlockList,
                    CombatMaidConfig.Key_OpenSaveFolder,
                    CombatMaidConfig.Key_Button_RebuildWineFox
                },
                0.7f, false, false
            );

            #endregion
        }

        #region 辅助函数

        private static void OnCustomMaidNameChanged(string value)
        {
            CombatMaidConfig.CustomMaidName = value;
            CMDebug.Log($"女仆名称输入: {value}");
            CombatMaidConfig.ApplyCustomMaidName(value);
        }

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

        #endregion
    }
}