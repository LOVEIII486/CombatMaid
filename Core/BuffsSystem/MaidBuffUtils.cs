using System;
using System.Collections.Generic;
using UnityEngine;
using Duckov.Buffs;
using Duckov.Utilities;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// Buff ID 辅助工具
    /// </summary>
    public static class MaidBuffUtils
    {
        private static Dictionary<int, Buff> _buffCache;
        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化缓存
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            _buffCache = new Dictionary<int, Buff>();

            try
            {
                var buffsData = GameplayDataSettings.Buffs;
                if (buffsData == null)
                {
                    CMDebug.LogError("无法访问 GameplayDataSettings.Buffs");
                    return;
                }
                
                // 获取 allBuffs
                var allBuffsField = typeof(GameplayDataSettings.BuffsData).GetField("allBuffs", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                
                if (allBuffsField != null)
                {
                    var list = allBuffsField.GetValue(buffsData) as List<Buff>;
                    if (list != null)
                    {
                        foreach (var buff in list)
                        {
                            RegisterBuffToCache(buff);
                        }
                    }
                }

                CMDebug.LogInfo($"初始化完成，已缓存 {_buffCache.Count} 个原版 Buff");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"初始化失败: {ex}");
            }
        }

        private static void RegisterBuffToCache(Buff buff)
        {
            if (buff == null) return;
            if (!_buffCache.ContainsKey(buff.ID))
            {
                _buffCache.Add(buff.ID, buff);
                CMDebug.Log($"Buff录入: ID={buff.ID}, Name={buff.name}"); 
            }
        }

        /// <summary>
        /// 通过 ID 给目标施加 Buff
        /// </summary>
        public static bool ApplyBuffByID(CharacterMainControl target, int buffId, out string buffName, CharacterMainControl fromWho = null)
        {
            buffName = ""; // out 参数必须在使用前赋值
    
            if (!_isInitialized) Initialize();
            if (target == null || target.Health.IsDead) return false;

            if (_buffCache.TryGetValue(buffId, out Buff buffPrefab))
            {
                // 获取本地化名称
                buffName = buffPrefab.DisplayName; 
        
                target.AddBuff(buffPrefab, fromWho, 1);
                return true;
            }
            else
            {
                CMDebug.LogWarning($"未找到 ID 为 {buffId} 的 Buff");
                return false;
            }
        }
    }
}