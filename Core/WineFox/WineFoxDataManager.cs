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