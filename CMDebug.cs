using UnityEngine;
using System.IO;
using System.Runtime.CompilerServices; // 用于获取调用者信息
using CombatMaid.Settings;

namespace CombatMaid
{
    /// <summary>
    /// 全局调试工具
    /// </summary>
    public static class CMDebug
    {
        private const string Prefix = "[CombatMaid]";

        /// <summary>
        /// 普通调试日志
        /// </summary>
        public static void Log(object message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
        {
            if (!CombatMaidConfig.DebugMode) return;

            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            Debug.Log($"{Prefix}[{className}.{memberName}] {message ?? "null"}");
        }

        /// <summary>
        /// 警告日志
        /// </summary>
        public static void LogWarning(object message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
        {
            // if (!CombatMaidConfig.DebugMode) return;

            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            Debug.LogWarning($"{Prefix}[{className}.{memberName}] {message ?? "null"}");
        }

        /// <summary>
        /// 错误日志
        /// </summary>
        public static void LogError(object message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
        {
            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            Debug.LogError($"{Prefix}[{className}.{memberName}] {message ?? "null"}");
        }
    }
}