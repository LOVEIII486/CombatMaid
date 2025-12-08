using System;
using System.Collections;
using System.Collections.Generic;
using CombatMaid.Core.MaidSkillSystem.Skills;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CombatMaid.Core.MaidSkillSystem
{
    public static class MaidSkillFactory
    {
        /// <summary>
        /// 根据配置创建技能实例
        /// </summary>
        public static IMaidSkill CreateSkill(MaidSkillConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.SkillID)) return null;

            try
            {
                switch (config.SkillID)
                {
                    case "SelfHeal":
                        return new Skill_SelfHeal();

                    case "GrenadeThrow":
                        // 解析参数：获取 ItemID，默认为 67
                        int grenadeId = GetParam(config.Params, "ItemID", 67);
                        return new Skill_GrenadeThrower(grenadeId);

                    case "Buff":
                        // 解析参数：Buff名称和ID
                        string buffName = GetParam(config.Params, "BuffName", "");
                        int buffId = GetParam(config.Params, "BuffID", 0);
                        
                        if (!string.IsNullOrEmpty(buffName) && buffId > 0)
                        {
                            return new Skill_BuffPlayer(buffName, buffId);
                        }
                        CMDebug.LogWarning($"技能 {config.SkillID} 参数缺失: 需要 BuffName 和 BuffID");
                        return null;

                    case "VanillaBuff":
                        // 解析整数列表参数 "BuffIDs"
                        List<int> buffIds = GetListParam<int>(config.Params, "BuffIDs");
                        
                        if (buffIds != null && buffIds.Count > 0)
                        {
                            return new Skill_VanillaBuff(buffIds);
                        }
                        
                        CMDebug.LogWarning($"技能 VanillaBuff 配置无效: 缺少 BuffIDs 参数或列表为空");
                        return null;
                    
                    default:
                        CMDebug.LogWarning($"未知的技能类型: {config.SkillID}");
                        return null;
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"创建技能 {config.SkillID} 失败: {ex.Message}");
                return null;
            }
        }
        
        private static List<T> GetListParam<T>(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out object val)) 
                return new List<T>();

            var result = new List<T>();

            try
            {
                // 情况1: JSON.NET 解析出的 JArray
                if (val is JArray jArray)
                {
                    foreach (var item in jArray)
                    {
                        try { result.Add(item.ToObject<T>()); } catch { }
                    }
                }
                // 情况2: 普通列表
                else if (val is IEnumerable list && !(val is string))
                {
                    foreach (var item in list)
                    {
                        try { result.Add((T)Convert.ChangeType(item, typeof(T))); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"解析列表参数 {key} 失败: {ex.Message}");
            }

            return result;
        }
        
        /// <summary>
        /// 辅助方法：安全地从字典获取参数
        /// </summary>
        private static T GetParam<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters == null || !parameters.ContainsKey(key)) return defaultValue;

            object val = parameters[key];
            try
            {
                // 处理 JSON 数值类型转换问题 (例如 long 转 int)
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)Convert.ToInt32(val);
                }
                if (typeof(T) == typeof(float))
                {
                    return (T)(object)Convert.ToSingle(val);
                }
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)val.ToString();
                }

                return (T)val;
            }
            catch
            {
                CMDebug.LogWarning($"参数 {key} 类型转换失败，使用默认值");
                return defaultValue;
            }
        }
    }
}