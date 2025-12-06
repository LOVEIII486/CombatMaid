using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CombatMaid.Core.MaidConfigs;
using System.Reflection;

namespace CombatMaid.Core.WineFox
{
    /// <summary>
    /// 酒狐数据的持久化管理器
    /// </summary>
    public static class WineFoxDataManager
    {
        private const string SaveFileName = "WineFox_Data.json";
        private const string DefaultPresetName = "ContractMaid_WineFox.json";
        
        // 缓存当前的数据实例
        public static MaidProfileData CurrentData { get; private set; }

        public static string GetSaveDir()
        {
            // 获取 DLL 所在目录
            string modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(modDir, "Saves");
        }

        /// <summary>
        /// 加载或初始化数据
        /// </summary>
        public static MaidProfileData LoadOrInit()
        {
            string saveDir = GetSaveDir();
            string savePath = Path.Combine(saveDir, SaveFileName);

            // 1. 尝试加载存档
            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    CurrentData = JsonConvert.DeserializeObject<MaidProfileData>(json);
                    CMDebug.Log($"[WineFox] 成功加载存档数据");
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"[WineFox] 存档损坏，回退到默认: {ex.Message}");
                }
            }

            // 2. 如果没有存档或加载失败，读取默认预设
            if (CurrentData == null)
            {
                string modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string defaultPath = Path.Combine(modDir, "MaidPreset", DefaultPresetName);

                if (File.Exists(defaultPath))
                {
                    string json = File.ReadAllText(defaultPath);
                    CurrentData = JsonConvert.DeserializeObject<MaidProfileData>(json);
                    CMDebug.Log($"[WineFox] 已初始化默认数据");
                }
                else
                {
                    CMDebug.LogError($"[WineFox] 严重错误：找不到默认配置文件 {defaultPath}");
                    return null;
                }
            }
            
            // 3. [关键] 应用技能树加成
            // 注意：我们传入的是数据的深拷贝或在应用时确保不修改原始存档字段，
            // 或者明确区分 "BaseStats" 和 "RuntimeStats"。
            // 这里为了简单，直接修改 CurrentData 的内存值用于生成，
            // 但在保存时你需要决定是否要保存这些加成（通常建议只保存等级/经验，属性动态计算）。
            ApplySkillTreeBonuses(CurrentData);

            return CurrentData;
        }

        /// <summary>
        /// 保存数据
        /// </summary>
        public static void SaveData()
        {
            if (CurrentData == null) return;

            try
            {
                string saveDir = GetSaveDir();
                if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

                string savePath = Path.Combine(saveDir, SaveFileName);
                
                // 序列化
                string json = JsonConvert.SerializeObject(CurrentData, Formatting.Indented);
                File.WriteAllText(savePath, json);
                
                CMDebug.Log($"[WineFox] 数据已保存");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[WineFox] 保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// [核心逻辑] 将技能树的加成应用到面板上
        /// </summary>
        private static void ApplySkillTreeBonuses(MaidProfileData data)
        {
            if (data == null || data.PresetConfig == null) return;

            // 假设你有一个 SkillTreeManager
            // var bonuses = SkillTreeManager.Instance.GetGlobalBonuses(); 

            // 示例：模拟应用技能树加成
            // 1. 属性加成
            // data.PresetConfig.Health *= (1 + bonuses.HealthPercent);
            // data.PresetConfig.DamageMultiplier += bonuses.DamageAdd;

            // 2. 技能注入
            // 如果技能树解锁了 "高级治疗"，则注入到 Skills 列表
            /*
            if (SkillTreeManager.Instance.IsSkillUnlocked("AdvancedHeal"))
            {
                bool hasSkill = data.ExtraData.Skills.Exists(s => s.SkillID == "AdvancedHeal");
                if (!hasSkill)
                {
                    data.ExtraData.Skills.Add(new MaidSkillConfig { SkillID = "AdvancedHeal" });
                }
            }
            */
            
            CMDebug.Log("[WineFox] 已应用技能树加成（示例逻辑）");
        }
    }
}