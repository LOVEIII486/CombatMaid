using CombatMaid.ModSettingsApi;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using CombatMaid.Core;
using CombatMaid.Core.WineFox;

namespace CombatMaid.Settings
{
    public static class CombatMaidConfig
    {
        // ==================== 配置项 Key ====================
        
        // 属性倍率
        public const string Key_HealthMultiplier = "HealthMultiplier";
        public const string Key_AttackMultiplier = "AttackMultiplier";
        public const string Key_MoveSpeedMultiplier = "MoveSpeedMultiplier";
        
        // 按键配置
        public const string Key_Bind_Move = "KeyBind_Move";
        public const string Key_Bind_Heal = "KeyBind_Heal";
        public const string Key_Bind_Hold = "KeyBind_Hold";
        
        // 自定义女仆配置
        public const string Key_CustomMaidName = "CustomMaidName";
        public const string Key_CustomMaidModelID = "CustomMaidModelID";
        
        // ==================== 本地化 Key ====================
        
        // 组标题
        public const string LocalKey_Group_Stats = "Settings_CombatMaid_Stats";
        public const string LocalKey_Group_Keys = "Settings_CombatMaid_Keys";
        public const string LocalKey_Group_Customize = "Settings_CombatMaid_Customize";
        
        // 自定义女仆配置项
        public const string LocalKey_CustomMaidName = "Settings_CustomMaidName";
        public const string LocalKey_CustomMaidModelID = "Settings_CustomMaidModelID";
        public const string LocalKey_OpenSaveFolder = "Settings_OpenSaveFolder";
        public const string LocalKey_OpenSaveFolderButton = "Settings_OpenSaveFolderButton";

        // ==================== 默认值 ====================
        
        private const float Default_HealthMultiplier = 1.0f;
        private const float Default_AttackMultiplier = 1.0f;
        private const float Default_MoveSpeedMultiplier = 1.0f;
        
        private const string Default_CustomMaidName = "";
        private const string Default_CustomMaidModelID = "";

        // ==================== 静态变量 ====================
        
        public static float HealthMultiplier { get; set; } = Default_HealthMultiplier;
        public static float AttackMultiplier { get; set; } = Default_AttackMultiplier;
        public static float MoveSpeedMultiplier { get; set; } = Default_MoveSpeedMultiplier;
        
        public static KeyCode KeyMove { get; set; } = KeyCode.G;
        public static KeyCode KeyHeal { get; set; } = KeyCode.H;
        public static KeyCode KeyHold { get; set; } = KeyCode.J;
        
        // 自定义女仆配置
        public static string CustomMaidName { get; set; } = Default_CustomMaidName;
        public static string CustomMaidModelID { get; set; } = Default_CustomMaidModelID;

        // ==================== 独立更新函数 ====================
        
        /// <summary>
        /// 仅更新女仆名称
        /// </summary>
        public static bool ApplyCustomMaidName(string newName)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(newName))
            {
                CMDebug.LogWarning("[Config] 女仆名称不能为空，跳过应用");
                return false;
            }

            // 加载存档
            var data = LoadWineFoxData();
            if (data == null) return false;

            // 应用名称
            string trimmedName = newName.Trim();
            if (data.PresetConfig.CustomName == trimmedName)
            {
                CMDebug.Log("[Config] 名称无变化，跳过保存");
                return false;
            }

            data.PresetConfig.CustomName = trimmedName;
            
            // 保存
            return SaveWineFoxData(data, $"已更新女仆名称: {trimmedName}");
        }
        
        /// <summary>
        /// 仅更新模型ID
        /// </summary>
        public static bool ApplyCustomModelID(string newModelID)
        {
            // 验证输入
            if (!string.IsNullOrWhiteSpace(newModelID))
            {
                if (!int.TryParse(newModelID, out int testId))
                {
                    CMDebug.LogWarning($"[Config] 无效的模型ID: {newModelID}，必须是纯数字");
                    return false;
                }
                
                if (testId < 0)
                {
                    CMDebug.LogWarning("[Config] 模型ID不能为负数");
                    return false;
                }
            }

            // 加载存档
            var data = LoadWineFoxData();
            if (data == null) return false;

            if (data.ExtraData == null)
            {
                data.ExtraData = new MaidExtraInfo();
            }

            // 应用模型ID
            string targetModelID = string.IsNullOrWhiteSpace(newModelID) ? "" : newModelID.Trim();
            
            if (data.ExtraData.CustomModelID == targetModelID)
            {
                CMDebug.Log("[Config] 模型ID无变化，跳过保存");
                return false;
            }

            data.ExtraData.CustomModelID = targetModelID;
            
            // 保存
            string logMsg = string.IsNullOrEmpty(targetModelID) 
                ? "已清除自定义模型ID（使用默认模型）" 
                : $"已更新模型ID: {targetModelID}";
            
            return SaveWineFoxData(data, logMsg);
        }

        // ==================== 辅助函数 ====================
        
        /// <summary>
        /// 加载酒狐存档
        /// </summary>
        private static MaidProfileData LoadWineFoxData()
        {
            var data = WineFoxDataManager.CurrentData ?? WineFoxDataManager.LoadOrInit();
            
            if (data == null)
            {
                CMDebug.LogWarning("[Config] 酒狐存档尚未生成，请先生成一次酒狐后再修改配置");
                return null;
            }

            if (data.PresetConfig == null)
            {
                CMDebug.LogError("[Config] 酒狐存档数据异常：PresetConfig 为 null");
                return null;
            }

            return data;
        }
        
        /// <summary>
        /// 保存酒狐存档
        /// </summary>
        private static bool SaveWineFoxData(MaidProfileData data, string logMessage)
        {
            try
            {
                WineFoxDataManager.SaveData();
                // 刷新 Spawner 缓存
                if (MaidSpawner.Instance != null)
                {
                    MaidSpawner.Instance.RefreshWineFoxCache();
                }
                
                CMDebug.Log($"[Config] ✓ {logMessage}");
                return true;
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[Config] 保存存档失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 从酒狐存档加载配置到 UI
        /// </summary>
        public static void LoadCustomConfigFromWineFox()
        {
            var data = WineFoxDataManager.CurrentData ?? WineFoxDataManager.LoadOrInit();
            
            if (data == null || data.PresetConfig == null)
            {
                CMDebug.LogWarning("[Config] 酒狐存档不存在，使用空白配置");
                CustomMaidName = "";
                CustomMaidModelID = "";
                return;
            }
            CustomMaidName = data.PresetConfig.CustomName ?? "";
            if (data.ExtraData != null && !string.IsNullOrEmpty(data.ExtraData.CustomModelID))
            {
                CustomMaidModelID = data.ExtraData.CustomModelID;
            }
            else
            {
                CustomMaidModelID = "";
            }
           
            CMDebug.Log($"[Config] 已从存档加载配置: 名称={CustomMaidName}, 模型={CustomMaidModelID}");
        }
        
        /// <summary>
        /// 加载配置
        /// </summary>
        public static void Load()
        {
            if (ModSettingAPI.GetSavedValue(Key_HealthMultiplier, out float savedHp) && savedHp > 0) 
                HealthMultiplier = savedHp;
            if (ModSettingAPI.GetSavedValue(Key_AttackMultiplier, out float savedAtk) && savedAtk > 0) 
                AttackMultiplier = savedAtk;
            if (ModSettingAPI.GetSavedValue(Key_MoveSpeedMultiplier, out float savedSpeed) && savedSpeed > 0) 
                MoveSpeedMultiplier = savedSpeed;
            
            if (ModSettingAPI.GetSavedValue(Key_Bind_Move, out KeyCode k1)) KeyMove = k1;
            if (ModSettingAPI.GetSavedValue(Key_Bind_Heal, out KeyCode k2)) KeyHeal = k2;
            if (ModSettingAPI.GetSavedValue(Key_Bind_Hold, out KeyCode k3)) KeyHold = k3;
            
            bool hasModSettingValues = false;
            
            if (ModSettingAPI.GetSavedValue(Key_CustomMaidName, out string savedName))
            {
                CustomMaidName = savedName;
                hasModSettingValues = true;
            }
            if (ModSettingAPI.GetSavedValue(Key_CustomMaidModelID, out string savedModelId))
            {
                CustomMaidModelID = savedModelId;
                hasModSettingValues = true;
            }
            
            if (!hasModSettingValues || 
                (string.IsNullOrEmpty(CustomMaidName) && 
                 string.IsNullOrEmpty(CustomMaidModelID)))
            {
                LoadCustomConfigFromWineFox();
            }
        }
    }
}