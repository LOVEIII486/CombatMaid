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
        private const string Key_DebugMode = "DebugMode";
        
        // [新增] 按键配置 Key
        public const string Key_Bind_Move = "KeyBind_Move";
        public const string Key_Bind_Heal = "KeyBind_Heal";
        public const string Key_Bind_Hold = "KeyBind_Hold";

        // ==================== 默认值 ====================
        private const bool Default_EnableMaidMode = true;
        private const float Default_AttackMultiplier = 1.0f;
        private const int Default_MoveSpeed = 5;
        private const bool Default_DebugMode = false;

        // ==================== 静态变量 ====================
        public static bool EnableMaidMode { get; set; } = Default_EnableMaidMode;
        public static float AttackMultiplier { get; set; } = Default_AttackMultiplier;
        public static int MoveSpeed { get; set; } = Default_MoveSpeed;
        public static bool DebugMode { get; set; } = Default_DebugMode;
        
        // [新增] 运行时按键变量 (带默认值)
        public static KeyCode KeyMove { get; set; } = KeyCode.G;
        public static KeyCode KeyHeal { get; set; } = KeyCode.H;
        public static KeyCode KeyHold { get; set; } = KeyCode.F;

        /// <summary>
        /// 加载配置
        /// </summary>
        public static void Load()
        {
            if (ModSettingAPI.GetSavedValue(Key_EnableMaidMode, out bool savedMode))
                EnableMaidMode = savedMode;

            if (ModSettingAPI.GetSavedValue(Key_AttackMultiplier, out float savedAtk))
                if (savedAtk > 0) AttackMultiplier = savedAtk;

            if (ModSettingAPI.GetSavedValue(Key_MoveSpeed, out int savedSpeed))
                MoveSpeed = savedSpeed;
            
            if (ModSettingAPI.GetSavedValue(Key_DebugMode, out bool savedDebug))
                DebugMode = savedDebug;
            
            // [新增] 加载按键绑定
            if (ModSettingAPI.GetSavedValue(Key_Bind_Move, out KeyCode savedKeyMove))
                KeyMove = savedKeyMove;
            
            if (ModSettingAPI.GetSavedValue(Key_Bind_Heal, out KeyCode savedKeyHeal))
                KeyHeal = savedKeyHeal;
                
            if (ModSettingAPI.GetSavedValue(Key_Bind_Hold, out KeyCode savedKeyHold))
                KeyHold = savedKeyHold;
        }
    }
}