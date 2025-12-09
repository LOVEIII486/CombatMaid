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
            AddAttribute, // 增加属性（如 MaxHealth +50）
            MultiplyAttribute, // 乘法属性（如 DamageMultiplier *1.2）
            AddSkill, // 添加技能
            UnlockItem, // 解锁物品（已有，但也可以在这里统一）
            CustomLogic // 自定义逻辑（预留）
        }

        public ModifierType Type;

        // 用于属性修改
        public string AttributeKey; // 例如 "Health", "MoveSpeedFactor"
        public float AttributeValue; // 修改值

        // 用于技能添加
        public string SkillID; // 例如 "AutoHeal", "Grenade"
        public Dictionary<string, object> SkillParams; // 技能参数

        // 用于自定义逻辑
        public string CustomActionID;
    }

    /// <summary>
    /// 通用的 PerkBehaviour - 解锁时修改酒狐存档
    /// </summary>
    public class ModifyWineFoxDataBehaviour : PerkBehaviour
    {
        // 这些字段会在 SkillTreeBuilder 中注入
        public List<SkillTreeModifier> Modifiers = new List<SkillTreeModifier>();
        public string NodeID; // 用于调试和日志

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

            // 确保 ExtraData 存在（兼容旧存档）
            if (data.ExtraData == null) data.ExtraData = new MaidExtraInfo();
            if (data.ExtraData.AppliedModifierKeys == null) data.ExtraData.AppliedModifierKeys = new List<string>();

            bool anyChangesMade = false;

            for (int i = 0; i < Modifiers.Count; i++)
            {
                var modifier = Modifiers[i];

                // [关键设计] 生成唯一 Key: "{NodeID}#{Index}"
                // 这种格式既绑定了节点，也区分了同一节点下的多个修改项
                string uniqueKey = $"{NodeID}#{i}";

                // 3. 检查是否已应用
                if (data.ExtraData.AppliedModifierKeys.Contains(uniqueKey))
                {
                    CMDebug.Log($"[ModifyWineFoxData] 跳过已应用修改: {uniqueKey}");
                    continue;
                }

                // 4. 尝试应用
                if (ApplyModifier(modifier, data))
                {
                    // 5. 标记为已应用
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

            // 使用反射修改对应字段
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

            // --- 通用升级逻辑：解析 "Upgrade_..._{OldID}_{NewID}" ---
            // 示例: "Upgrade_254_258" 或 "Upgrade_Weapon_254_258"
            if (!string.IsNullOrEmpty(action) && action.StartsWith("Upgrade_"))
            {
                string[] parts = action.Split('_');
        
                // 确保至少有3部分 (Upgrade, OldID, NewID)，并解析最后两个为整数
                if (parts.Length >= 3 &&
                    int.TryParse(parts[parts.Length - 2], out int oldId) &&
                    int.TryParse(parts[parts.Length - 1], out int newId))
                {
                    if (data.PresetConfig.CustomItemIDs == null) 
                        data.PresetConfig.CustomItemIDs = new List<int>();

                    var list = data.PresetConfig.CustomItemIDs;
                    int index = list.IndexOf(oldId);

                    if (index != -1)
                    {
                        list[index] = newId;
                        CMDebug.Log($"  [Custom] 物品升级成功: {oldId} -> {newId} (Index: {index})");
                        return true;
                    }
                    else
                    {
                        // 没找到旧物品 -> 保底补发
                        if (!list.Contains(newId))
                        {
                            list.Add(newId);
                            CMDebug.Log($"  [Custom] 未找到旧物品{oldId}，已补发新物品{newId}");
                            return true;
                        }
                    }
                    return false;
                }
            }
            
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
            
            switch (action)
            {
                case "UnlockEliteWeapons":
                    return true;
            
                default:
                    CMDebug.LogWarning($"  [Custom] 未知的或格式错误的 Action: {action}");
                    return false;
            }
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

                // ---------------------------------------------------------
                // 情况 A: 简单整数列表 (ItemIDs, BuffIDs)
                // ---------------------------------------------------------
                if (key == "ItemIDs" || key == "BuffIDs")
                {
                    // 1. 提取旧列表
                    List<int> currentList = new List<int>();
                    if (skill.Params.TryGetValue(key, out object oldVal))
                    {
                        if (oldVal is JArray jArray) currentList = jArray.ToObject<List<int>>();
                        else if (oldVal is List<int> list) currentList = list;
                    }

                    // 2. 提取新列表
                    List<int> newList = new List<int>();
                    if (newVal is JArray jNewArray) newList = jNewArray.ToObject<List<int>>();
                    else if (newVal is List<int> list) newList = list;

                    // 3. 合并去重 (int 直接比较值)
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
                // ---------------------------------------------------------
                // 情况 B: 复杂对象列表 (Buffs)
                // ---------------------------------------------------------
                else if (key == "Buffs")
                {
                    // 定义一个临时结构来辅助解析 (Name, ID)
                    // 1. 提取旧列表
                    var currentBuffs = new List<MaidSkillFactory.BuffParamEntry>();
                    if (skill.Params.TryGetValue(key, out object oldVal))
                    {
                        if (oldVal is JArray jArray)
                            currentBuffs = jArray.ToObject<List<MaidSkillFactory.BuffParamEntry>>();
                        else if (oldVal is List<MaidSkillFactory.BuffParamEntry> list) currentBuffs = list;
                    }

                    // 2. 提取新列表
                    var newBuffs = new List<MaidSkillFactory.BuffParamEntry>();
                    if (newVal is JArray jNewArray)
                        newBuffs = jNewArray.ToObject<List<MaidSkillFactory.BuffParamEntry>>();
                    else if (newVal is List<MaidSkillFactory.BuffParamEntry> list) newBuffs = list;

                    // 3. 合并去重 (根据 BuffName 判断是否存在)
                    int addedCount = 0;
                    foreach (var newEntry in newBuffs)
                    {
                        // 如果旧列表中不存在同名 Buff，则添加
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
                // ---------------------------------------------------------
                // 情况 C: 其他参数 (直接覆盖)
                // ---------------------------------------------------------
                else
                {
                    if (!skill.Params.ContainsKey(key) || !skill.Params[key].Equals(newVal))
                    {
                        skill.Params[key] = newVal;
                        hasChanges = true;
                        CMDebug.Log($"    -> 更新参数 {key}");
                    }
                }
            }

            return hasChanges;
        }

        #endregion
    }

    /// <summary>
    /// 玩家属性修改器（保持原有功能，但添加日志）
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