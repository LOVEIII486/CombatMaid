using System;
using System.Collections.Generic;
using UnityEngine;
using Duckov.PerkTrees.Behaviours;
using CombatMaid.Core.WineFox;
using CombatMaid.Core.MaidConfigs;
using Duckov.PerkTrees;

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
            AddAttribute,      // 增加属性（如 MaxHealth +50）
            MultiplyAttribute, // 乘法属性（如 DamageMultiplier *1.2）
            AddSkill,          // 添加技能
            UnlockItem,        // 解锁物品（已有，但也可以在这里统一）
            CustomLogic        // 自定义逻辑（预留）
        }

        public ModifierType Type;
        
        // 用于属性修改
        public string AttributeKey;   // 例如 "Health", "MoveSpeedFactor"
        public float AttributeValue;  // 修改值
        
        // 用于技能添加
        public string SkillID;        // 例如 "AutoHeal", "Grenade"
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
                    float newValue = floatVal * multiplier;
                    field.SetValue(config, newValue);
                    CMDebug.Log($"  [Multiply] {key}: {floatVal} * {multiplier} = {newValue}");
                    return true;
                }
            }

            CMDebug.LogWarning($"[ModifyWineFoxData] 未找到属性: {key}");
            return false;
        }

        private bool ApplyAddSkill(SkillTreeModifier modifier, MaidProfileData data)
        {
            if (data.ExtraData == null)
            {
                data.ExtraData = new MaidExtraInfo();
            }

            if (data.ExtraData.Skills == null)
            {
                data.ExtraData.Skills = new List<MaidSkillConfig>();
            }

            // 检查是否已存在
            bool exists = data.ExtraData.Skills.Exists(s => s.SkillID == modifier.SkillID);
            if (exists)
            {
                CMDebug.LogWarning($"  [AddSkill] 技能 {modifier.SkillID} 已存在，跳过");
                return false;
            }

            // 添加技能
            var skillConfig = new MaidSkillConfig
            {
                SkillID = modifier.SkillID,
                Params = modifier.SkillParams ?? new Dictionary<string, object>()
            };

            data.ExtraData.Skills.Add(skillConfig);
            CMDebug.Log($"  [AddSkill] 已添加技能: {modifier.SkillID}");
            return true;
        }

        private bool ApplyCustomLogic(SkillTreeModifier modifier, MaidProfileData data)
        {
            // 根据 CustomActionID 执行特定逻辑
            switch (modifier.CustomActionID)
            {
                case "UnlockEliteWeapons":
                    // 示例：解锁高级武器
                    if (data.PresetConfig.CustomItemIDs == null)
                    {
                        data.PresetConfig.CustomItemIDs = new List<int>();
                    }
                    data.PresetConfig.CustomItemIDs.Add(999); // 假设 999 是高级武器
                    CMDebug.Log($"  [Custom] 已解锁精英武器");
                    return true;

                default:
                    CMDebug.LogWarning($"  [Custom] 未知的自定义逻辑: {modifier.CustomActionID}");
                    return false;
            }
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
