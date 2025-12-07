using CombatMaid.ModSettingsApi;
using UnityEngine;

namespace CombatMaid.Settings
{
    public static class CombatMaidConfig
    {
        // ==================== 配置项 Key ====================
        public const string Key_EnableMaidMode = "EnableMaidMode";
        public const string Key_DebugMode = "DebugMode";
        
        // 属性倍率
        public const string Key_HealthMultiplier = "HealthMultiplier";
        public const string Key_AttackMultiplier = "AttackMultiplier";
        public const string Key_MoveSpeedMultiplier = "MoveSpeedMultiplier";
        
        // 按键配置
        public const string Key_Bind_Move = "KeyBind_Move";
        public const string Key_Bind_Heal = "KeyBind_Heal";
        public const string Key_Bind_Hold = "KeyBind_Hold";

        // ==================== 默认值 ====================
        private const bool Default_EnableMaidMode = true;
        private const bool Default_DebugMode = false;
        
        private const float Default_HealthMultiplier = 1.0f;
        private const float Default_AttackMultiplier = 1.0f;
        private const float Default_MoveSpeedMultiplier = 1.0f;

        // ==================== 静态变量 ====================
        public static bool EnableMaidMode { get; set; } = Default_EnableMaidMode;
        public static bool DebugMode { get; set; } = Default_DebugMode;
        
        public static float HealthMultiplier { get; set; } = Default_HealthMultiplier;
        public static float AttackMultiplier { get; set; } = Default_AttackMultiplier;
        public static float MoveSpeedMultiplier { get; set; } = Default_MoveSpeedMultiplier;
        
        public static KeyCode KeyMove { get; set; } = KeyCode.G;
        public static KeyCode KeyHeal { get; set; } = KeyCode.H;
        public static KeyCode KeyHold { get; set; } = KeyCode.F;

        /// <summary>
        /// 加载配置
        /// </summary>
        public static void Load()
        {
            // 开关
            if (ModSettingAPI.GetSavedValue(Key_EnableMaidMode, out bool savedMode)) EnableMaidMode = savedMode;
            if (ModSettingAPI.GetSavedValue(Key_DebugMode, out bool savedDebug)) DebugMode = savedDebug;

            // 数值
            if (ModSettingAPI.GetSavedValue(Key_HealthMultiplier, out float savedHp) && savedHp > 0) HealthMultiplier = savedHp;
            if (ModSettingAPI.GetSavedValue(Key_AttackMultiplier, out float savedAtk) && savedAtk > 0) AttackMultiplier = savedAtk;
            if (ModSettingAPI.GetSavedValue(Key_MoveSpeedMultiplier, out float savedSpeed) && savedSpeed > 0) MoveSpeedMultiplier = savedSpeed;
            
            // 按键
            if (ModSettingAPI.GetSavedValue(Key_Bind_Move, out KeyCode k1)) KeyMove = k1;
            if (ModSettingAPI.GetSavedValue(Key_Bind_Heal, out KeyCode k2)) KeyHeal = k2;
            if (ModSettingAPI.GetSavedValue(Key_Bind_Hold, out KeyCode k3)) KeyHold = k3;
        }
    }
}