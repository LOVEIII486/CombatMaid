using CombatMaid.ModSettingsApi;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        public const string Key_CustomMaidItems = "CustomMaidItems";
        
        // ==================== 本地化 Key ====================
        
        // 组标题
        public const string LocalKey_Group_Stats = "Settings_CombatMaid_Stats";
        public const string LocalKey_Group_Keys = "Settings_CombatMaid_Keys";
        public const string LocalKey_Group_Customize = "Settings_CombatMaid_Customize";
        
        // 自定义女仆配置项
        public const string LocalKey_CustomMaidName = "Settings_CustomMaidName";
        public const string LocalKey_CustomMaidModelID = "Settings_CustomMaidModelID";
        public const string LocalKey_CustomMaidItems = "Settings_CustomMaidItems";
        public const string LocalKey_OpenSaveFolder = "Settings_OpenSaveFolder";
        public const string LocalKey_OpenSaveFolderButton = "Settings_OpenSaveFolderButton";

        // ==================== 默认值 ====================
        
        private const float Default_HealthMultiplier = 1.0f;
        private const float Default_AttackMultiplier = 1.0f;
        private const float Default_MoveSpeedMultiplier = 1.0f;
        
        private const string Default_CustomMaidName = "";
        private const string Default_CustomMaidModelID = "";
        private const string Default_CustomMaidItems = "";

        // ==================== 静态变量 ====================
        
        public static float HealthMultiplier { get; set; } = Default_HealthMultiplier;
        public static float AttackMultiplier { get; set; } = Default_AttackMultiplier;
        public static float MoveSpeedMultiplier { get; set; } = Default_MoveSpeedMultiplier;
        
        public static KeyCode KeyMove { get; set; } = KeyCode.G;
        public static KeyCode KeyHeal { get; set; } = KeyCode.H;
        public static KeyCode KeyHold { get; set; } = KeyCode.F;
        
        // 自定义女仆配置
        public static string CustomMaidName { get; set; } = Default_CustomMaidName;
        public static string CustomMaidModelID { get; set; } = Default_CustomMaidModelID;
        public static string CustomMaidItems { get; set; } = Default_CustomMaidItems;

        // ==================== 辅助属性 ====================
        
        /// <summary>
        /// 获取自定义女仆的模型ID（整数）
        /// </summary>
        public static int GetCustomModelIDAsInt()
        {
            if (int.TryParse(CustomMaidModelID, out int id))
            {
                return id;
            }
            return 0; // 0 表示无效或不使用自定义模型
        }
        
        /// <summary>
        /// 获取自定义女仆的物品ID列表
        /// </summary>
        public static List<int> GetCustomItemIDs()
        {
            var result = new List<int>();
            
            if (string.IsNullOrWhiteSpace(CustomMaidItems))
            {
                return result;
            }
            
            // 分割字符串，支持中英文逗号和空格
            var parts = CustomMaidItems.Split(new[] { ',', '，', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out int itemId))
                {
                    result.Add(itemId);
                }
                else
                {
                    CMDebug.LogWarning($"[Config] 无效的物品ID: {part}");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 验证自定义女仆配置的完整性
        /// </summary>
        public static bool ValidateCustomMaidConfig(out string errorMsg)
        {
            errorMsg = "";
            
            // 1. 名称不能为空
            if (string.IsNullOrWhiteSpace(CustomMaidName))
            {
                errorMsg = "女仆名称不能为空";
                return false;
            }
            
            // 2. 如果填写了模型ID，必须是有效数字
            if (!string.IsNullOrWhiteSpace(CustomMaidModelID))
            {
                if (!int.TryParse(CustomMaidModelID, out int modelId))
                {
                    errorMsg = "模型ID必须是有效的数字";
                    return false;
                }
                
                if (modelId < 0)
                {
                    errorMsg = "模型ID不能为负数";
                    return false;
                }
            }
            
            // 3. 如果填写了物品列表，检查格式
            if (!string.IsNullOrWhiteSpace(CustomMaidItems))
            {
                var items = GetCustomItemIDs();
                if (items.Count == 0)
                {
                    errorMsg = "物品ID格式错误，请使用逗号分隔的数字（例如: 254,258,300）";
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public static void Load()
        {
            // 数值倍率
            if (ModSettingAPI.GetSavedValue(Key_HealthMultiplier, out float savedHp) && savedHp > 0) 
                HealthMultiplier = savedHp;
            if (ModSettingAPI.GetSavedValue(Key_AttackMultiplier, out float savedAtk) && savedAtk > 0) 
                AttackMultiplier = savedAtk;
            if (ModSettingAPI.GetSavedValue(Key_MoveSpeedMultiplier, out float savedSpeed) && savedSpeed > 0) 
                MoveSpeedMultiplier = savedSpeed;
            
            // 按键
            if (ModSettingAPI.GetSavedValue(Key_Bind_Move, out KeyCode k1)) KeyMove = k1;
            if (ModSettingAPI.GetSavedValue(Key_Bind_Heal, out KeyCode k2)) KeyHeal = k2;
            if (ModSettingAPI.GetSavedValue(Key_Bind_Hold, out KeyCode k3)) KeyHold = k3;
            
            // 自定义女仆配置
            if (ModSettingAPI.GetSavedValue(Key_CustomMaidName, out string name)) 
                CustomMaidName = name;
            if (ModSettingAPI.GetSavedValue(Key_CustomMaidModelID, out string modelId)) 
                CustomMaidModelID = modelId;
            if (ModSettingAPI.GetSavedValue(Key_CustomMaidItems, out string items)) 
                CustomMaidItems = items;
        }
    }
}