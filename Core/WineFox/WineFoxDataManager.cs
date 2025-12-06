using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CombatMaid.Core.MaidConfigs;
using System.Reflection;
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
        private const string SaveFolderName = "CombatMaidSaves"; // 新的存档文件夹名
        
        public static MaidProfileData CurrentData { get; private set; }

        // 序列化设置 (保留之前的修复)
        private static readonly JsonSerializerSettings _saveSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new UnityStructResolver()
        };

        /// <summary>
        /// [修改] 获取存档目录：指向游戏根目录/CombatMaidSaves
        /// </summary>
        public static string GetSaveDir()
        {
            // Directory.GetCurrentDirectory() 通常就是游戏的 .exe 所在目录
            string gameRoot = Directory.GetCurrentDirectory();
            return Path.Combine(gameRoot, SaveFolderName);
        }

        /// <summary>
        /// 获取 Mod 安装目录 (用于读取只读的默认预设)
        /// </summary>
        private static string GetModDir()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static MaidProfileData LoadOrInit()
        {
            // 1. 尝试从【游戏根目录】加载玩家存档
            string saveDir = GetSaveDir();
            string savePath = Path.Combine(saveDir, SaveFileName);

            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    CurrentData = JsonConvert.DeserializeObject<MaidProfileData>(json);
                    CMDebug.Log($"[WineFox] 成功加载存档: {savePath}");
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"[WineFox] 存档损坏: {ex.Message}");
                }
            }

            // 2. 如果无存档，从【Mod目录】读取默认预设
            if (CurrentData == null)
            {
                string modDir = GetModDir();
                string defaultPath = Path.Combine(modDir, "MaidPreset", DefaultPresetName);

                if (File.Exists(defaultPath))
                {
                    string json = File.ReadAllText(defaultPath);
                    CurrentData = JsonConvert.DeserializeObject<MaidProfileData>(json);
                    CMDebug.Log($"[WineFox] 已初始化默认数据 (源: {DefaultPresetName})");
                }
                else
                {
                    CMDebug.LogError($"[WineFox] 严重错误：找不到默认配置文件 {defaultPath}");
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
                
                CMDebug.Log($"[WineFox] 数据已保存至: {savePath}");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[WineFox] 保存失败: {ex.Message}");
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