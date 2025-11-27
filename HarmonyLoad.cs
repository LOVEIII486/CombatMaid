using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CombatMaid
{
    /// <summary>
    /// Harmony 库加载器
    /// </summary>
    public static class HarmonyLoad
    {
        private const string LogTag = "[CombatMaid.HarmonyLoad]";
        private static Assembly _harmonyAssembly;

        /// <summary>
        /// 获取或检查 Harmony 程序集是否已加载
        /// </summary>
        public static Assembly LoadHarmony()
        {
            if (_harmonyAssembly != null)
            {
                return _harmonyAssembly;
            }

            _harmonyAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a =>
                {
                    string name = a.GetName().Name;
                    return name.Equals("0Harmony", StringComparison.OrdinalIgnoreCase) ||
                           name.Equals("HarmonyLib", StringComparison.OrdinalIgnoreCase);
                });

            if (_harmonyAssembly != null)
            {
                Debug.Log($"{LogTag} 检测到环境已加载 Harmony: {_harmonyAssembly.FullName}");
                return _harmonyAssembly;
            }

            Debug.LogError($"{LogTag} 严重错误: 未找到 Harmony 库！请确保已安装依赖库或 Mod 加载器提供了 Harmony 环境。");
            return null;
        }
    }
}