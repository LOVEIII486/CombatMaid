using System;
using System.Collections.Generic;
using UnityEngine;
using Duckov.PerkTrees.Behaviours;
using CombatMaid.Core.WineFox;
using CombatMaid.Core.MaidConfigs;
using CombatMaid.Core.MaidSkillSystem;
using Duckov.PerkTrees;
using Newtonsoft.Json.Linq;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 技能树修改器 - 定义如何修改存档数据
    /// </summary>
    [Serializable]
    public class SkillTreeModifier
    {
        public enum ModifierType
        {
            AddAttribute, // 增加属性
            MultiplyAttribute, // 乘法属性
            AddSkill, // 添加技能
            UnlockItem, // 解锁物品
            CustomLogic, // 自定义逻辑
            SetVector2
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
    /// 通用的 PerkBehaviour - 解锁时修改酒狐存档
    /// </summary>
    public class ModifyWineFoxDataBehaviour : PerkBehaviour
    {
        public List<SkillTreeModifier> Modifiers = new List<SkillTreeModifier>();
        public string NodeID;

        protected override void OnUnlocked()
        {
            if (Modifiers == null || Modifiers.Count == 0)
            {
                CMDebug.LogWarning($"[ModifyWineFoxData] 节点 {NodeID} 没有配置任何修改器");
                return;
            }

            // 加载酒狐数据
            var data = WineFoxDataManager.CurrentData ?? WineFoxDataManager.LoadOrInit();
            if (data == null)
            {
                CMDebug.LogError($"[ModifyWineFoxData] 无法加载酒狐数据");
                return;
            }

            if (data.PresetConfig == null)
            {
                CMDebug.LogError($"[ModifyWineFoxData] 酒狐数据的 PresetConfig 为 null");
                return;
            }

            if (data.ExtraData == null) data.ExtraData = new MaidExtraInfo();
            if (data.ExtraData.AppliedModifierKeys == null) data.ExtraData.AppliedModifierKeys = new List<string>();

            bool anyChangesMade = false;

            for (int i = 0; i < Modifiers.Count; i++)
            {
                var modifier = Modifiers[i];

                // 生成唯一 Key: "{NodeID}#{Index}"
                string uniqueKey = $"{NodeID}#{i}";

                // 检查是否已应用
                if (data.ExtraData.AppliedModifierKeys.Contains(uniqueKey))
                {
                    CMDebug.Log($"[ModifyWineFoxData] 跳过已应用修改: {uniqueKey}");
                    continue;
                }

                if (ApplyModifier(modifier, data))
                {
                    data.ExtraData.AppliedModifierKeys.Add(uniqueKey);
                    anyChangesMade = true;
                    CMDebug.Log($"[ModifyWineFoxData] 应用成功: {uniqueKey} ({modifier.Type})");
                }
            }

            if (anyChangesMade)
            {
                WineFoxDataManager.SaveData();
                MaidSpawner.Instance.RefreshWineFoxCache();
                CMDebug.Log($"[ModifyWineFoxData] ✓ 节点 {NodeID} 数据更新并已保存");
            }
            else
            {
                CMDebug.Log($"[ModifyWineFoxData] 节点 {NodeID} 无需更新（已全部应用）");
            }
        }

        private bool ApplyModifier(SkillTreeModifier modifier, MaidProfileData data)
        {
            try
            {
                switch (modifier.Type)
                {
                    case SkillTreeModifier.ModifierType.AddAttribute:
                        return ApplyAddAttribute(modifier, data.PresetConfig);

                    case SkillTreeModifier.ModifierType.MultiplyAttribute:
                        return ApplyMultiplyAttribute(modifier, data.PresetConfig);

                    case SkillTreeModifier.ModifierType.AddSkill:
                        return ApplyAddSkill(modifier, data);

                    case SkillTreeModifier.ModifierType.CustomLogic:
                        return ApplyCustomLogic(modifier, data);
                    
                    case SkillTreeModifier.ModifierType.SetVector2:
                        return ApplySetVector2(modifier, data.PresetConfig);

                    default:
                        CMDebug.LogWarning($"[ModifyWineFoxData] 未处理的修改器类型: {modifier.Type}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[ModifyWineFoxData] 应用修改器失败: {ex.Message}");
                return false;
            }
        }

        #region 修改器实现

        private bool ApplyAddAttribute(SkillTreeModifier modifier, MaidConfig config)
        {
            string key = modifier.AttributeKey;
            float value = modifier.AttributeValue;

            var field = typeof(MaidConfig).GetField(key);
            if (field != null)
            {
                object currentValue = field.GetValue(config);

                if (currentValue is float floatVal)
                {
                    field.SetValue(config, floatVal + value);
                    CMDebug.Log($"  [Add] {key}: {floatVal} -> {floatVal + value}");
                    return true;
                }
                else if (currentValue is int intVal)
                {
                    field.SetValue(config, intVal + (int)value);
                    CMDebug.Log($"  [Add] {key}: {intVal} -> {intVal + (int)value}");
                    return true;
                }
            }

            CMDebug.LogWarning($"[ModifyWineFoxData] 未找到属性: {key}");
            return false;
        }

        private bool ApplyMultiplyAttribute(SkillTreeModifier modifier, MaidConfig config)
        {
            string key = modifier.AttributeKey;
            float multiplier = modifier.AttributeValue;

            var field = typeof(MaidConfig).GetField(key);
            if (field != null)
            {
                object currentValue = field.GetValue(config);

                if (currentValue is float floatVal)
                {
                    float rawValue = floatVal * multiplier;
                    float newValue = (float)Math.Round(rawValue, 3); // 截断到3位
            
                    field.SetValue(config, newValue);
                    CMDebug.Log($"  [Multiply] {key}: {floatVal} * {multiplier} = {newValue} (Raw: {rawValue})");
                    return true;
                }
            }

            CMDebug.LogWarning($"[ModifyWineFoxData] 未找到属性: {key}");
            return false;
        }

        private bool ApplyAddSkill(SkillTreeModifier modifier, MaidProfileData data)
        {
            if (data.ExtraData == null) data.ExtraData = new MaidExtraInfo();
            if (data.ExtraData.Skills == null) data.ExtraData.Skills = new List<MaidSkillConfig>();

            // 1. 查找是否存在同名技能
            var existingSkill = data.ExtraData.Skills.Find(s => s.SkillID == modifier.SkillID);

            // 2. 如果已存在 -> 进入参数合并模式 
            if (existingSkill != null)
            {
                CMDebug.Log($"  [AddSkill] 技能 {modifier.SkillID} 已存在，尝试合并参数...");
                return MergeSkillParams(existingSkill, modifier.SkillParams);
            }

            // 3. 如果不存在 -> 创建新技能
            var skillConfig = new MaidSkillConfig
            {
                SkillID = modifier.SkillID,
                Params = modifier.SkillParams ?? new Dictionary<string, object>()
            };

            data.ExtraData.Skills.Add(skillConfig);
            CMDebug.Log($"  [AddSkill] 已添加新技能: {modifier.SkillID}");
            return true;
        }

        private bool ApplyCustomLogic(SkillTreeModifier modifier, MaidProfileData data)
        {
            string action = modifier.CustomActionID;

            // if (!string.IsNullOrEmpty(action) && action.StartsWith("Upgrade_"))
            // {
            //     string[] parts = action.Split('_');
            //
            //     // 确保至少有3部分 (Upgrade, OldID, NewID)，并解析最后两个为整数
            //     if (parts.Length >= 3 &&
            //         int.TryParse(parts[parts.Length - 2], out int oldId) &&
            //         int.TryParse(parts[parts.Length - 1], out int newId))
            //     {
            //         if (data.PresetConfig.CustomItemIDs == null) 
            //             data.PresetConfig.CustomItemIDs = new List<int>();
            //
            //         var list = data.PresetConfig.CustomItemIDs;
            //         int index = list.IndexOf(oldId);
            //
            //         if (index != -1)
            //         {
            //             list[index] = newId;
            //             CMDebug.Log($"  [Custom] 物品升级成功: {oldId} -> {newId} (Index: {index})");
            //             return true;
            //         }
            //         else
            //         {
            //             // 没找到旧物品 -> 保底补发
            //             if (!list.Contains(newId))
            //             {
            //                 list.Add(newId);
            //                 CMDebug.Log($"  [Custom] 未找到旧物品{oldId}，已补发新物品{newId}");
            //                 return true;
            //             }
            //         }
            //         return false;
            //     }
            // }
            
            if (!string.IsNullOrEmpty(action) && action.StartsWith("SetBool_"))
            {
                string[] parts = action.Split('_');
                if (parts.Length == 3)
                {
                    string fieldName = parts[1];
                    bool targetValue = parts[2].Equals("True", StringComparison.OrdinalIgnoreCase);

                    // 反射查找 MaidConfig 中的布尔字段
                    var field = typeof(MaidConfig).GetField(fieldName);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        field.SetValue(data.PresetConfig, targetValue);
                        CMDebug.Log($"  [Custom] 布尔属性修改: {fieldName} -> {targetValue}");
                        return true;
                    }
                    else
                    {
                        CMDebug.LogWarning($"  [Custom] 未找到布尔字段: {fieldName}");
                    }
                    return false;
                }
            }
            
            if (!string.IsNullOrEmpty(action) && action == "SyncInventoryCapacity")
            {
                // 如果是运行时立即生效，我们需要找到当前活跃的女仆并强制设置。
                if (MaidManager.Instance != null)
                {
                    var activeMaid = MaidManager.Instance.GetActiveWineFox();
                    if (activeMaid != null && activeMaid.MaidCharacter != null)
                    {
                        // 计算新的容量 (Base + Modifiers)
                        // 注意：这里 data.PresetConfig.InventoryCapacity 已经是修改后的值（因为 AddAttribute 先执行）
                        int newCap = Mathf.RoundToInt(data.PresetConfig.InventoryCapacity);
                        if (newCap > 0 && activeMaid.MaidCharacter.CharacterItem?.Inventory != null)
                        {
                            activeMaid.MaidCharacter.CharacterItem.Inventory.SetCapacity(newCap);
                            CMDebug.Log($"[Custom] 运行时背包扩容已应用: {newCap}");
                        }
                    }
                }
                return true; 
            }
            
            switch (action)
            {
                case "UnlockEliteWeapons":
                    return true;
            
                default:
                    CMDebug.LogWarning($"  [Custom] 未知的或格式错误的 Action: {action}");
                    return false;
            }
        }
        
        private bool ApplySetVector2(SkillTreeModifier modifier, MaidConfig config)
        {
            string key = modifier.AttributeKey;
            var field = typeof(MaidConfig).GetField(key);
    
            if (field != null && field.FieldType == typeof(Vector2))
            {
                field.SetValue(config, modifier.VectorValue);
                CMDebug.Log($"  [SetVector2] {key} 已成功修改为: {modifier.VectorValue}");
                return true;
            }
            CMDebug.LogWarning($"[ModifyWineFoxData] 属性 {key} 不是 Vector2 或不存在");
            return false;
        }
        
        #endregion

        #region 辅助函数
        
        /// <summary>
        /// 合并技能参数 (ItemIDs/BuffIDs 整数列表合并，Buffs 对象列表合并)
        /// </summary>
        private bool MergeSkillParams(MaidSkillConfig skill, Dictionary<string, object> newParams)
        {
            if (newParams == null || newParams.Count == 0) return false;

            bool hasChanges = false;

            foreach (var kvp in newParams)
            {
                string key = kvp.Key;
                object newVal = kvp.Value;

                // 简单整数列表 (ItemIDs, BuffIDs)
                if (key == "ItemIDs" || key == "BuffIDs")
                {
                    List<int> currentList = new List<int>();
                    if (skill.Params.TryGetValue(key, out object oldVal))
                    {
                        if (oldVal is JArray jArray) currentList = jArray.ToObject<List<int>>();
                        else if (oldVal is List<int> list) currentList = list;
                    }

                    List<int> newList = new List<int>();
                    if (newVal is JArray jNewArray) newList = jNewArray.ToObject<List<int>>();
                    else if (newVal is List<int> list) newList = list;

                    int addedCount = 0;
                    foreach (int id in newList)
                    {
                        if (!currentList.Contains(id))
                        {
                            currentList.Add(id);
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        skill.Params[key] = currentList;
                        hasChanges = true;
                        CMDebug.Log($"    -> [{key}] 追加了 {addedCount} 个 ID");
                    }
                }
                // 复杂对象列表 (Buffs)
                else if (key == "Buffs")
                {
                    var currentBuffs = new List<MaidSkillFactory.BuffParamEntry>();
                    if (skill.Params.TryGetValue(key, out object oldVal))
                    {
                        if (oldVal is JArray jArray)
                            currentBuffs = jArray.ToObject<List<MaidSkillFactory.BuffParamEntry>>();
                        else if (oldVal is List<MaidSkillFactory.BuffParamEntry> list) currentBuffs = list;
                    }

                    var newBuffs = new List<MaidSkillFactory.BuffParamEntry>();
                    if (newVal is JArray jNewArray)
                        newBuffs = jNewArray.ToObject<List<MaidSkillFactory.BuffParamEntry>>();
                    else if (newVal is List<MaidSkillFactory.BuffParamEntry> list) newBuffs = list;

                    int addedCount = 0;
                    foreach (var newEntry in newBuffs)
                    {
                        if (!currentBuffs.Exists(b => b.BuffName == newEntry.BuffName))
                        {
                            currentBuffs.Add(newEntry);
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        skill.Params[key] = currentBuffs;
                        hasChanges = true;
                        CMDebug.Log($"    -> [{key}] 追加了 {addedCount} 个 Buff");
                    }
                }
                // 其他参数 (直接覆盖)
                else
                {
                    if (!skill.Params.ContainsKey(key) || !skill.Params[key].Equals(newVal))
                    {
                        skill.Params[key] = newVal;
                        hasChanges = true;
                        CMDebug.Log($"    -> 覆盖更新参数 {key}");
                    }
                }
            }

            return hasChanges;
        }

        #endregion
    }

    /// <summary>
    /// 玩家属性修改器
    /// </summary>
    public class ModifyPlayerCharacterStats : ModifyCharacterStatsBase
    {
        public override string Description => string.Empty;

        protected override void OnUnlocked()
        {
            base.OnUnlocked();
            CMDebug.Log($"[PlayerStats] 玩家属性修改已应用");
        }
    }
}