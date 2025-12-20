using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 技能树配置加载器
    /// </summary>
    public static class SkillTreeConfigLoader
    {
        private const string CONFIG_FOLDER = "SkillTree";
        
        /// <summary>
        /// 从指定文件加载技能树配置
        /// </summary>
        public static SkillTreeConfig LoadFromFile(string modPath, string fileName)
        {
            try
            {
                string fullPath = Path.Combine(modPath, CONFIG_FOLDER, fileName);
                
                if (!File.Exists(fullPath))
                {
                    CMDebug.LogError($"[SkillTreeConfigLoader] 配置文件不存在: {fullPath}");
                    return null;
                }

                string json = File.ReadAllText(fullPath);
                var config = JsonConvert.DeserializeObject<SkillTreeConfig>(json);

                if (config == null)
                {
                    CMDebug.LogError($"[SkillTreeConfigLoader] 解析配置失败: {fileName}");
                    return null;
                }

                if (!ValidateConfig(config))
                {
                    CMDebug.LogError($"[SkillTreeConfigLoader] 配置验证失败: {fileName}");
                    return null;
                }

                CMDebug.LogInfo($"[SkillTreeConfigLoader] 成功加载技能树: {config.TreeName} (共 {config.Nodes.Count} 个节点)");
                return config;
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[SkillTreeConfigLoader] 加载配置异常: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 加载 Skills 文件夹下的所有技能树配置
        /// </summary>
        public static List<SkillTreeConfig> LoadAllConfigs(string modPath)
        {
            var configs = new List<SkillTreeConfig>();
            string configDir = Path.Combine(modPath, CONFIG_FOLDER);

            if (!Directory.Exists(configDir))
            {
                CMDebug.LogWarning($"[SkillTreeConfigLoader] 配置目录不存在，将创建: {configDir}");
                Directory.CreateDirectory(configDir);
                return configs;
            }

            string[] files = Directory.GetFiles(configDir, "*.json");
            CMDebug.Log($"[SkillTreeConfigLoader] 找到 {files.Length} 个配置文件");

            foreach (string file in files)
            {
                var config = LoadFromFile(modPath, Path.GetFileName(file));
                if (config != null)
                {
                    configs.Add(config);
                }
            }

            return configs;
        }

        /// <summary>
        /// 将配置转换为 SkillNodeDef 列表
        /// </summary>
        public static List<SkillNodeDef> ConvertToNodeDefs(SkillTreeConfig config)
        {
            var defs = new List<SkillNodeDef>();

            foreach (var nodeConfig in config.Nodes)
            {
                try
                {
                    var def = new SkillNodeDef
                    {
                        ID = nodeConfig.ID,
                        DisplayName = nodeConfig.DisplayName,
                        Description = nodeConfig.Description,
                        IconFileName = nodeConfig.IconFileName,
                        
                        // 成本配置
                        CostMoney = nodeConfig.CostMoney,
                        RequiredLevel = nodeConfig.RequiredLevel,
                        CostItems = nodeConfig.CostItems ?? new Dictionary<int, int>(),
                        
                        UnlockTime = nodeConfig.UnlockTime,
                        
                        // 位置和前置
                        Position = nodeConfig.Position,
                        PrerequisiteIDs = nodeConfig.PrerequisiteIDs ?? new List<string>(),
                        
                        // 属性修改器
                        PlayerStatModifiers = nodeConfig.PlayerStatModifiers ?? new Dictionary<string, float>(),
                        
                        // 直接转换修改器
                        MaidModifiers = ConvertToModifiers(nodeConfig)
                    };

                    defs.Add(def);
                }
                catch (Exception ex)
                {
                    CMDebug.LogError($"[SkillTreeConfigLoader] 转换节点失败 {nodeConfig?.ID}: {ex.Message}");
                }
            }

            return defs;
        }

        /// <summary>
        /// 将配置转换为修改器列表
        /// </summary>
        private static List<SkillTreeModifier> ConvertToModifiers(SkillNodeConfig config)
        {
            var modifiers = new List<SkillTreeModifier>();
            if (config.MaidModifiers != null && config.MaidModifiers.Count > 0)
            {
                foreach (var modConfig in config.MaidModifiers)
                {
                    var modifier = ConvertSingleModifier(modConfig);
                    if (modifier != null)
                    {
                        modifiers.Add(modifier);
                    }
                }
            }

            return modifiers;
        }

        /// <summary>
        /// 转换单个修改器配置
        /// </summary>
        private static SkillTreeModifier ConvertSingleModifier(MaidModifierConfig config)
        {
            var modifier = new SkillTreeModifier();

            switch (config.Type?.ToLower())
            {
                case "addattribute":
                case "add":
                    modifier.Type = SkillTreeModifier.ModifierType.AddAttribute;
                    modifier.AttributeKey = config.Attribute;
                    modifier.AttributeValue = config.Value;
                    break;

                case "multiplyattribute":
                case "multiply":
                    modifier.Type = SkillTreeModifier.ModifierType.MultiplyAttribute;
                    modifier.AttributeKey = config.Attribute;
                    modifier.AttributeValue = config.Value;
                    break;

                case "addskill":
                case "skill":
                    modifier.Type = SkillTreeModifier.ModifierType.AddSkill;
                    modifier.SkillID = config.SkillID;
                    modifier.SkillParams = config.SkillParams ?? new Dictionary<string, object>();
                    break;

                case "custom":
                    modifier.Type = SkillTreeModifier.ModifierType.CustomLogic;
                    modifier.CustomActionID = config.CustomAction;
                    break;
                
                case "setvector2":
                    modifier.Type = SkillTreeModifier.ModifierType.SetVector2;
                    modifier.AttributeKey = config.Attribute;
                    modifier.VectorValue = config.VectorValue;
                    break;

                default:
                    CMDebug.LogWarning($"[SkillTreeConfigLoader] 未知的修改器类型: {config.Type}");
                    return null;
            }

            return modifier;
        }

        /// <summary>
        /// 验证配置完整性
        /// </summary>
        private static bool ValidateConfig(SkillTreeConfig config)
        {
            if (string.IsNullOrEmpty(config.TreeID))
            {
                CMDebug.LogError("TreeID 不能为空");
                return false;
            }

            if (config.Nodes == null || config.Nodes.Count == 0)
            {
                CMDebug.LogError("至少需要一个技能节点");
                return false;
            }

            var idSet = new HashSet<string>();
            foreach (var node in config.Nodes)
            {
                if (string.IsNullOrEmpty(node.ID))
                {
                    CMDebug.LogError("发现节点ID为空");
                    return false;
                }

                if (idSet.Contains(node.ID))
                {
                    CMDebug.LogError($"重复的节点ID: {node.ID}");
                    return false;
                }

                idSet.Add(node.ID);
            }

            foreach (var node in config.Nodes)
            {
                if (node.PrerequisiteIDs != null)
                {
                    foreach (var prereqId in node.PrerequisiteIDs)
                    {
                        if (!idSet.Contains(prereqId))
                        {
                            CMDebug.LogError($"节点 {node.ID} 引用了不存在的前置技能: {prereqId}");
                            return false;
                        }
                    }
                }
            }

            if (HasCircularDependency(config.Nodes))
            {
                CMDebug.LogError("检测到循环依赖");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检测循环依赖
        /// </summary>
        private static bool HasCircularDependency(List<SkillNodeConfig> nodes)
        {
            var graph = new Dictionary<string, List<string>>();
            
            foreach (var node in nodes)
            {
                if (!graph.ContainsKey(node.ID))
                {
                    graph[node.ID] = new List<string>();
                }

                if (node.PrerequisiteIDs != null)
                {
                    graph[node.ID].AddRange(node.PrerequisiteIDs);
                }
            }

            var visited = new HashSet<string>();
            var recStack = new HashSet<string>();

            foreach (var nodeId in graph.Keys)
            {
                if (DFS(nodeId, graph, visited, recStack))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DFS(string nodeId, Dictionary<string, List<string>> graph, 
            HashSet<string> visited, HashSet<string> recStack)
        {
            if (recStack.Contains(nodeId)) return true;
            if (visited.Contains(nodeId)) return false;

            visited.Add(nodeId);
            recStack.Add(nodeId);

            if (graph.ContainsKey(nodeId))
            {
                foreach (var neighbor in graph[nodeId])
                {
                    if (DFS(neighbor, graph, visited, recStack))
                    {
                        return true;
                    }
                }
            }

            recStack.Remove(nodeId);
            return false;
        }
    }
}