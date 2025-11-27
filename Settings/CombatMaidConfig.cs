using CombatMaid.ModSettingsApi;
using UnityEngine;

namespace CombatMaid.Settings
{
    public static class CombatMaidConfig
    {
        // ==================== 配置项 Key ====================
        private const string Key_EnableMaidMode = "EnableMaidMode";
        private const string Key_AttackMultiplier = "AttackMultiplier";
        private const string Key_MoveSpeed = "MoveSpeed";

        // ==================== 默认值 ====================
        private const bool Default_EnableMaidMode = true;
        private const float Default_AttackMultiplier = 1.0f;
        private const int Default_MoveSpeed = 5;

        // ==================== 静态变量 ====================
        public static bool EnableMaidMode { get; set; } = Default_EnableMaidMode;
        public static float AttackMultiplier { get; set; } = Default_AttackMultiplier;
        public static int MoveSpeed { get; set; } = Default_MoveSpeed;

        /// <summary>
        /// 加载配置
        /// </summary>
        public static void Load()
        {
            if (ModSettingAPI.GetSavedValue(Key_EnableMaidMode, out bool savedMode))
            {
                EnableMaidMode = savedMode;
            }

            if (ModSettingAPI.GetSavedValue(Key_AttackMultiplier, out float savedAtk))
            {
                if (savedAtk > 0) AttackMultiplier = savedAtk;
            }

            if (ModSettingAPI.GetSavedValue(Key_MoveSpeed, out int savedSpeed))
            {
                MoveSpeed = savedSpeed;
            }
        }
    }
}