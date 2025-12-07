using UnityEngine;
using System.IO;
using System.Runtime.CompilerServices;

namespace CombatMaid
{
    /// <summary>
    /// 全局日志工具
    /// </summary>
    public static class CMDebug
    {
        private const bool ShowDebugLogs = true; 

        private const string BasePrefix = "[CombatMaid]";

        /// <summary>
        /// [调试日志] 
        /// </summary>
        public static void Log(object message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
        {
            if (!ShowDebugLogs) return;
            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            Debug.Log($"{BasePrefix}[Debug][{className}.{memberName}] {message ?? "null"}");
        }

        /// <summary>
        /// [核心日志] 
        /// </summary>
        public static void LogInfo(object message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
        {
            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            Debug.Log($"{BasePrefix}[Info][{className}.{memberName}] {message ?? "null"}");
        }

        /// <summary>
        /// [警告日志]
        /// </summary>
        public static void LogWarning(object message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
        {
            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            Debug.LogWarning($"{BasePrefix}[Warn][{className}.{memberName}] {message ?? "null"}");
        }

        /// <summary>
        /// [错误日志]
        /// </summary>
        public static void LogError(object message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
        {
            string className = Path.GetFileNameWithoutExtension(sourceFilePath);
            Debug.LogError($"{BasePrefix}[Error][{className}.{memberName}] {message ?? "null"}");
        }
    }
}