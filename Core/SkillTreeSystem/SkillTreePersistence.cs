using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using CombatMaid; // 引用 CMDebug 和 ModBehaviour
using UnityEngine;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 技能树存档数据结构
    /// </summary>
    [System.Serializable]
    public class SkillTreeSaveData
    {
        public List<string> UnlockedNodeIDs = new List<string>();
    }

    public static class SkillTreePersistence
    {
        private const string SaveFileName = "MaidSkillTreeSave.json";

        /// <summary>
        /// 获取存档完整路径
        /// </summary>
        private static string GetSavePath()
        {
            string root = ModBehaviour.Instance != null ? ModBehaviour.Instance.ModRootPath : ".";
            string dir = Path.Combine(root, "SkillTree/Saves");
            
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, SaveFileName);
        }

        public static void Save(SkillTreeSaveData data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetSavePath(), json);
                CMDebug.Log($"[SkillTreePersistence] 存档已保存: {data.UnlockedNodeIDs.Count} 个节点");
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[SkillTreePersistence] 保存失败: {ex.Message}");
            }
        }

        public static SkillTreeSaveData Load()
        {
            string path = GetSavePath();
            if (!File.Exists(path))
            {
                return new SkillTreeSaveData();
            }

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<SkillTreeSaveData>(json);
                return data ?? new SkillTreeSaveData();
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"[SkillTreePersistence] 读取失败: {ex.Message}");
                return new SkillTreeSaveData();
            }
        }
    }
}