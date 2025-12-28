using System;
using System.Collections;
using System.Collections.Generic;
using CombatMaid.Core.BuffsSystem;
using CombatMaid.Core.MaidSkillSystem.Skills;
using Newtonsoft.Json.Linq;

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
                    case "BuffPlayer":
                        return CreateBuffPlayer(config.Params);
                    case "VanillaBuff":
                        return CreateVanillaBuff(config.Params);
                    case "EmergencyHeal":
                        return CreateEmergencyHeal(config.Params);
                    case "TeaBreak":
                        return new Skill_TeaBreak();
                    case "AutoRepairArmor":
                        return new Skill_AutoRepairArmor();
                    case "FreezeNova":
                        return new Skill_FreezeNova();
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
                    if (string.IsNullOrEmpty(p.BuffName)) continue;

                    int finalId = p.BuffID;
                    if (finalId == 0)
                    {
                        var effect = MaidBuffRegistry.Instance.GetEffect(p.BuffName);
                        if (effect != null)
                        {
                            finalId = effect.BuffID;
                        }
                        else
                        {
                            CMDebug.LogWarning($"未找到注册的 Buff 效果: {p.BuffName}");
                            continue;
                        }
                    }

                    buffList.Add((p.BuffName, finalId));
                }
            }

            if (buffList.Count > 0)
            {
                return new Skill_BuffPlayer(buffList);
            }

            CMDebug.LogWarning($"配置无效: 没有找到有效的 Buff");
            return null;
        }

        private static IMaidSkill CreateVanillaBuff(Dictionary<string, object> parameters)
        {
            List<int> buffIds = GetListParam<int>(parameters, "BuffIDs");

            if (buffIds != null && buffIds.Count > 0)
            {
                return new Skill_VanillaBuff(buffIds);
            }

            CMDebug.LogWarning($"配置无效: 'BuffIDs' 列表缺失或为空");
            return null;
        }
        
        private static IMaidSkill CreateEmergencyHeal(Dictionary<string, object> parameters)
        {
            string buffName = GetParam(parameters, "BuffName", "MaidBuff_SuperRegen");
            int buffId = GetParam(parameters, "BuffID", 888002);
            float duration = GetParam(parameters, "Duration", 15.0f);
            if (duration < 10f) { duration = 10f; } // 用于兼容旧版本存档5s，现在加强到持续10s

            return new Skill_EmergencyHeal(buffName, buffId, duration);
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