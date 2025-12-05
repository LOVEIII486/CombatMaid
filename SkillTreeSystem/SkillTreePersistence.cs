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
        // 已解锁/已购买的技能节点ID列表
        public List<string> UnlockedNodeIDs = new List<string>();
        
        // 如果未来有技能点系统，可以在这里加:
        // public int AvailablePoints;
    }

    public static class SkillTreePersistence
    {
        private const string SaveFileName = "SkillTreeData.json";

        /// <summary>
        /// 获取存档完整路径
        /// </summary>
        private static string GetSavePath()
        {
            // 确保 ModBehaviour.Instance.ModRootPath 有效，否则回退到当前目录
            string root = ModBehaviour.Instance != null ? ModBehaviour.Instance.ModRootPath : ".";
            string dir = Path.Combine(root, "Saves");
            
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
                return new SkillTreeSaveData(); // 返回新档
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