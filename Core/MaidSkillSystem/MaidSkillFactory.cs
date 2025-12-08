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
        /// 解析 JSON 列表中的 Buff 对象
        /// </summary>
        public class BuffParamEntry
        {
            public string BuffName;
            public int BuffID;
        }

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
                        return CreateSelfHeal();
                    case "GrenadeThrow":
                        return CreateGrenadeThrower(config.Params);
                    case "Buff":
                        return CreateBuffPlayer(config.Params);
                    case "VanillaBuff":
                        return CreateVanillaBuff(config.Params);
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

        #region 技能构建

        private static IMaidSkill CreateSelfHeal()
        {
            return new Skill_SelfHeal();
        }

        private static IMaidSkill CreateGrenadeThrower(Dictionary<string, object> parameters)
        {
            List<int> grenadeIds = GetListParam<int>(parameters, "ItemIDs");

            if (grenadeIds.Count == 0)
            {
                grenadeIds.Add(67); 
            }

            return new Skill_GrenadeThrower(grenadeIds);
        }

        private static IMaidSkill CreateBuffPlayer(Dictionary<string, object> parameters)
        {
            var buffList = new List<(string, int)>();
            
            var listParams = GetListParam<BuffParamEntry>(parameters, "Buffs");
            if (listParams != null)
            {
                foreach (var p in listParams)
                {
                    if (!string.IsNullOrEmpty(p.BuffName) && p.BuffID > 0)
                    {
                        buffList.Add((p.BuffName, p.BuffID));
                    }
                }
            }

            if (buffList.Count > 0)
            {
                return new Skill_BuffPlayer(buffList);
            }

            CMDebug.LogWarning($"[CreateBuffPlayer] 配置无效: 'Buffs' 列表为空或格式错误");
            return null;
        }

        private static IMaidSkill CreateVanillaBuff(Dictionary<string, object> parameters)
        {
            List<int> buffIds = GetListParam<int>(parameters, "BuffIDs");

            if (buffIds != null && buffIds.Count > 0)
            {
                return new Skill_VanillaBuff(buffIds);
            }

            CMDebug.LogWarning($"[CreateVanillaBuff] 配置无效: 'BuffIDs' 列表缺失或为空");
            return null;
        }

        #endregion

        #region 参数解析

        private static List<T> GetListParam<T>(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out object val)) 
                return new List<T>();

            var result = new List<T>();
            try
            {
                if (val is JArray jArray)
                {
                    foreach (var item in jArray)
                    {
                        try { result.Add(item.ToObject<T>()); } catch { }
                    }
                }
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
        
        private static T GetParam<T>(Dictionary<string, object> parameters, string key, T defaultValue)
        {
            if (parameters == null || !parameters.ContainsKey(key)) return defaultValue;
            object val = parameters[key];
            try
            {
                if (typeof(T) == typeof(int)) return (T)(object)Convert.ToInt32(val);
                if (typeof(T) == typeof(float)) return (T)(object)Convert.ToSingle(val);
                if (typeof(T) == typeof(string)) return (T)(object)val.ToString();
                return (T)val;
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion
    }
}