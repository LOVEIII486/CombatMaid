using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Reflection;
using CombatMaid.Core.SkillTreeSystem;
using Newtonsoft.Json.Serialization;

namespace CombatMaid.Core.WineFox
{
    /// <summary>
    /// 酒狐数据的持久化管理器
    /// </summary>
    public static class WineFoxDataManager
    {
        private const string SaveFileName = "WineFox_Data.json";
        private const string DefaultPresetName = "ContractMaid_WineFox.json";
        private const string SaveFolderName = "CombatMaidSaves";

        public static MaidProfileData CurrentData { get; private set; }

        private static readonly JsonSerializerSettings _saveSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new UnityStructResolver()
        };

        /// <summary>
        /// 获取存档目录：根目录/CombatMaidSaves
        /// </summary>
        public static string GetSaveDir()
        {
            string gameRoot = Directory.GetCurrentDirectory();
            return Path.Combine(gameRoot, SaveFolderName);
        }

        /// <summary>
        /// 获取 Mod 安装目录
        /// </summary>
        private static string GetModDir()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static MaidProfileData LoadOrInit()
        {
            string saveDir = GetSaveDir();
            string savePath = Path.Combine(saveDir, SaveFileName);

            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    CurrentData = JsonConvert.DeserializeObject<MaidProfileData>(json);
                    CMDebug.Log($"成功加载存档: {savePath}");
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"存档损坏: {ex.Message}");
                }
            }

            // 如果无存档，从Mod目录读取默认预设
            if (CurrentData == null)
            {
                string modDir = GetModDir();
                string defaultPath = Path.Combine(modDir, "MaidPreset", DefaultPresetName);

                if (File.Exists(defaultPath))
                {
                    string json = File.ReadAllText(defaultPath);
                    CurrentData = JsonConvert.DeserializeObject<MaidProfileData>(json);
                    CMDebug.Log($"已初始化默认数据 (源: {DefaultPresetName})");
                }
                else
                {
                    CMDebug.LogError($"严重错误：找不到默认配置文件 {defaultPath}");
                    return null;
                }
            }

            return CurrentData;
        }

        public static void SaveData()
        {
            if (CurrentData == null) return;

            try
            {
                string saveDir = GetSaveDir();
                if (!Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);

                string savePath = Path.Combine(saveDir, SaveFileName);

                string json = JsonConvert.SerializeObject(CurrentData, _saveSettings);
                File.WriteAllText(savePath, json);

                CMDebug.Log($"数据已保存至: {savePath}");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 基于最新平衡性配置重算属性
        /// </summary>
        public static void RebuildWineFoxSaveData()
        {
            var data = CurrentData ?? LoadOrInit();
            if (data == null || data.PresetConfig == null) return;

            string savedName = data.PresetConfig.CustomName;
            CMDebug.LogInfo("开始执行存档安全重构...");

            try
            {
                // 加载白板预设并覆盖
                string modDir = GetModDir();
                string defaultPath = Path.Combine(modDir, "MaidPreset", DefaultPresetName);
                string json = File.ReadAllText(defaultPath);
                var freshProfile = JsonConvert.DeserializeObject<MaidProfileData>(json);

                data.PresetConfig = freshProfile.PresetConfig;
                data.PresetConfig.CustomName = savedName; // 还原姓名

                // 清空并重算技能加成
                if (data.ExtraData == null) data.ExtraData = new MaidExtraInfo();
                if (data.ExtraData.AppliedModifierKeys == null) data.ExtraData.AppliedModifierKeys = new List<string>();
                data.ExtraData.AppliedModifierKeys.Clear();

                var skillSave = SkillTreePersistence.Load();
                var unlockedIds = skillSave?.UnlockedNodeIDs ?? new List<string>();

                if (unlockedIds.Count > 0)
                {
                    var treeConfig = SkillTreeConfigLoader.LoadFromFile(modDir, "SkillTree_MaidTech.json");
                    if (treeConfig != null && treeConfig.Nodes != null)
                    {
                        var nodeMap = new Dictionary<string, SkillNodeConfig>();
                        foreach (var n in treeConfig.Nodes) nodeMap[n.ID] = n;

                        foreach (var id in unlockedIds)
                        {
                            if (nodeMap.TryGetValue(id, out var nodeCfg) && nodeCfg.MaidModifiers != null)
                            {
                                SkillTreeDataModifier.ApplyMaidModifiers(id, nodeCfg.MaidModifiers, data);
                            }
                        }
                    }
                }

                SaveData();
                if (MaidSpawner.Instance != null) MaidSpawner.Instance.RefreshWineFoxCache();

                CMDebug.LogInfo($"重构完成。同步了 {unlockedIds.Count} 个节点的加成。");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"重构失败: {ex.Message}");
            }
        }

        private class UnityStructResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                if (property.PropertyName == "normalized" ||
                    property.PropertyName == "magnitude" ||
                    property.PropertyName == "sqrMagnitude")
                {
                    property.Ignored = true;
                }

                return property;
            }
        }
    }
}