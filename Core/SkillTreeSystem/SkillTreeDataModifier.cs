using System;
using System.Collections.Generic;
using UnityEngine;
using Duckov.PerkTrees.Behaviours;
using CombatMaid.Core.WineFox;
using CombatMaid.Core.MaidConfigs;
using Duckov.PerkTrees;
using Newtonsoft.Json.Linq;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 技能树修改器数据结构
    /// </summary>
    [Serializable]
    public class SkillTreeModifier
    {
        public enum ModifierType
        {
            AddAttribute,      // 增加属性
            MultiplyAttribute, // 乘法属性
            AddSkill,          // 添加技能
            UnlockItem,        // 解锁物品
            CustomLogic,       // 自定义逻辑
            SetVector2         // 设置 Vector2
        }

        public ModifierType Type;
        public string AttributeKey;
        public float AttributeValue;
        public Vector2 VectorValue;
        public string SkillID;
        public Dictionary<string, object> SkillParams;
        public string CustomActionID;
    }

    /// <summary>
    /// 核心逻辑类：统一处理所有对 MaidProfileData 的修改逻辑
    /// </summary>
    public static class SkillTreeDataModifier
    {
        // --- 适配器 1: 处理来自行为组件的数据 (Unity 检查器版本) ---
        public static bool ApplyBehaviourModifiers(string nodeID, List<SkillTreeModifier> modifiers, MaidProfileData data)
        {
            if (data == null || modifiers == null) return false;
            bool changed = false;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                // 统一调用核心逻辑入口
                if (ApplyCore(data, $"{nodeID}#{i}", m.Type.ToString(), m.AttributeKey, m.AttributeValue, m.VectorValue, m.SkillID, m.SkillParams, m.CustomActionID))
                {
                    changed = true;
                }
            }
            return changed;
        }

        // --- 适配器 2: 处理来自 JSON 配置的数据 (重构功能版本，对应 SkillTreeConfig.cs) ---
        public static bool ApplyMaidModifiers(string nodeID, List<MaidModifierConfig> modifiers, MaidProfileData data)
        {
            if (data == null || modifiers == null) return false;
            bool changed = false;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                // 统一调用核心逻辑入口 (匹配 MaidModifierConfig 的字段名：Attribute, Value, CustomAction)
                if (ApplyCore(data, $"{nodeID}#{i}", m.Type, m.Attribute, m.Value, m.VectorValue, m.SkillID, m.SkillParams, m.CustomAction))
                {
                    changed = true;
                }
            }
            return changed;
        }

        // --- 核心逻辑入口：整个模组唯一的加成计算真理 ---
        private static bool ApplyCore(MaidProfileData data, string uniqueKey, string typeStr, string attrKey, float val, Vector2 vec, string skillId, Dictionary<string, object> skillParams, string actionId)
        {
            // 1. 初始化检查
            if (data.ExtraData == null) data.ExtraData = new MaidExtraInfo();
            if (data.ExtraData.AppliedModifierKeys == null) data.ExtraData.AppliedModifierKeys = new List<string>();

            // 2. 防止在单次重构或解锁中重复应用
            if (data.ExtraData.AppliedModifierKeys.Contains(uniqueKey)) return false;

            bool success = false;
            try
            {
                // 3. 执行具体修改逻辑
                switch (typeStr)
                {
                    case "AddAttribute":
                        success = Internal_AddAttribute(attrKey, val, data.PresetConfig);
                        break;

                    case "MultiplyAttribute":
                        success = Internal_MultiplyAttribute(attrKey, val, data.PresetConfig);
                        break;

                    case "AddSkill":
                        success = Internal_AddSkill(skillId, skillParams, data);
                        break;

                    case "SetVector2":
                        success = Internal_SetVector2(attrKey, vec, data.PresetConfig);
                        break;
                    
                    case "Custom":
                    case "CustomLogic":
                        success = Internal_CustomLogic(actionId, data);
                        break;
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[SkillTree] 执行修改器 {uniqueKey} 异常: {ex.Message}");
            }

            if (success)
            {
                data.ExtraData.AppliedModifierKeys.Add(uniqueKey);
            }
            return success;
        }

        #region 私有实现 (所有业务逻辑集中于此)

        private static bool Internal_AddAttribute(string key, float val, MaidConfig config)
        {
            var field = typeof(MaidConfig).GetField(key);
            if (field == null) return false;

            object current = field.GetValue(config);
            if (current is float f) field.SetValue(config, f + val);
            else if (current is int i) field.SetValue(config, i + (int)val);
            else return false;

            CMDebug.Log($"  [加成应用] {key} 增加 {val}");
            return true;
        }

        private static bool Internal_MultiplyAttribute(string key, float multiplier, MaidConfig config)
        {
            var field = typeof(MaidConfig).GetField(key);
            if (field == null) return false;

            if (field.GetValue(config) is float f)
            {
                float newVal = (float)Math.Round(f * multiplier, 3);
                field.SetValue(config, newVal);
                CMDebug.Log($"  [加成应用] {key} 乘系数 {multiplier} -> {newVal}");
                return true;
            }
            return false;
        }

        private static bool Internal_AddSkill(string skillId, Dictionary<string, object> skillParams, MaidProfileData data)
        {
            if (data.ExtraData.Skills == null) data.ExtraData.Skills = new List<MaidSkillConfig>();

            var existing = data.ExtraData.Skills.Find(s => s.SkillID == skillId);
            if (existing != null)
            {
                return MergeSkillParams(existing, skillParams);
            }

            data.ExtraData.Skills.Add(new MaidSkillConfig { 
                SkillID = skillId, 
                Params = skillParams ?? new Dictionary<string, object>() 
            });
            CMDebug.Log($"  [加成应用] 已添加/更新技能: {skillId}");
            return true;
        }

        private static bool Internal_SetVector2(string key, Vector2 vec, MaidConfig config)
        {
            var field = typeof(MaidConfig).GetField(key);
            if (field != null && field.FieldType == typeof(Vector2))
            {
                field.SetValue(config, vec);
                return true;
            }
            return false;
        }

        private static bool Internal_CustomLogic(string action, MaidProfileData data)
        {
            if (string.IsNullOrEmpty(action)) return false;

            // 处理 SetBool_FieldName_True/False
            if (action.StartsWith("SetBool_"))
            {
                string[] parts = action.Split('_');
                if (parts.Length == 3)
                {
                    var field = typeof(MaidConfig).GetField(parts[1]);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        field.SetValue(data.PresetConfig, parts[2].Equals("True", StringComparison.OrdinalIgnoreCase));
                        return true;
                    }
                }
            }

            // 处理背包容量同步
            if (action == "SyncInventoryCapacity" && MaidManager.Instance != null)
            {
                var maid = MaidManager.Instance.GetActiveWineFox();
                if (maid?.MaidCharacter?.CharacterItem?.Inventory != null)
                {
                    maid.MaidCharacter.CharacterItem.Inventory.SetCapacity(Mathf.RoundToInt(data.PresetConfig.InventoryCapacity));
                }
                return true;
            }

            return false;
        }

        private static bool MergeSkillParams(MaidSkillConfig skill, Dictionary<string, object> newParams)
        {
            if (newParams == null) return false;
            bool changed = false;

            foreach (var kvp in newParams)
            {
                // 针对 ID 列表进行特殊处理
                if (kvp.Key == "ItemIDs" || kvp.Key == "BuffIDs")
                {
                    List<int> currentList = GetAsListInt(skill.Params, kvp.Key);
                    List<int> incomingList = GetAsListInt(newParams, kvp.Key);

                    foreach (int id in incomingList)
                    {
                        if (!currentList.Contains(id))
                        {
                            currentList.Add(id);
                            changed = true;
                        }
                    }
                    skill.Params[kvp.Key] = currentList;
                }
                else
                {
                    // 普通参数维持覆盖逻辑
                    if (!skill.Params.ContainsKey(kvp.Key) || !skill.Params[kvp.Key].Equals(kvp.Value))
                    {
                        skill.Params[kvp.Key] = kvp.Value;
                        changed = true;
                    }
                }
            }
            return changed;
        }
        
        private static List<int> GetAsListInt(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out object val) || val == null)
                return new List<int>();

            if (val is JArray jArray)
            {
                return jArray.ToObject<List<int>>();
            }
            if (val is List<int> list)
            {
                return new List<int>(list);
            }
            if (val is IEnumerable<object> enumerable)
            {
                var result = new List<int>();
                foreach (var item in enumerable) result.Add(Convert.ToInt32(item));
                return result;
            }

            return new List<int>();
        }

        #endregion
    }
    
    /// <summary>
    /// 解锁行为类：适配 Unity Inspector 配置
    /// </summary>
    public class ModifyWineFoxDataBehaviour : PerkBehaviour
    {
        public List<SkillTreeModifier> Modifiers = new List<SkillTreeModifier>();
        public string NodeID;

        protected override void OnUnlocked()
        {
            var data = WineFoxDataManager.CurrentData ?? WineFoxDataManager.LoadOrInit();
            if (data == null) return;

            // 调用统一适配器入口：处理行为类列表
            if (SkillTreeDataModifier.ApplyBehaviourModifiers(NodeID, Modifiers, data))
            {
                WineFoxDataManager.SaveData();
                MaidSpawner.Instance?.RefreshWineFoxCache();
                CMDebug.Log($"[ModifyWineFoxData] 节点 {NodeID} 修改已应用并保存");
            }
        }
    }

    public class ModifyPlayerCharacterStats : ModifyCharacterStatsBase
    {
        public override string Description => string.Empty;
        protected override void OnUnlocked()
        {
            base.OnUnlocked();
            CMDebug.Log("[PlayerStats] 玩家属性修改已应用");
        }
    }
}