using System;
using System.Collections.Generic;
using CombatMaid.Core.MaidSkillSystem.Skills;
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
                    case "AutoHeal":
                        return new Skill_SelfHeal();

                    case "Grenade":
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

                    // 在这里扩展更多技能...
                    
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