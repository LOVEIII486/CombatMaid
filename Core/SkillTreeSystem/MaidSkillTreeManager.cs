using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Duckov.Economy;
using Duckov.PerkTrees;
using Saves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatMaid.Core.SkillTreeSystem
{
    public class MaidSkillTreeManager
    {
        public static MaidSkillTreeManager Instance { get; private set; }
        
        // 运行时持有的技能树实例
        private PerkTree _runtimeTree;
        // 缓存已解锁的ID，方便快速查询，避免频繁遍历UI对象
        private HashSet<string> _unlockedCache = new HashSet<string>();

        private readonly ModBehaviour _mod;
        private readonly string _modDir;

        public MaidSkillTreeManager(ModBehaviour mod)
        {
            _mod = mod;
            _modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Instance = this;
        }

        public void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Base_SceneV2" || scene.name == "Base")
            {
                _mod.StartCoroutine(BuildTreeRoutine());
            }
        }

        // --- 公共 API：查询技能状态 ---

        /// <summary>
        /// 查询某个技能节点是否已解锁
        /// </summary>
        /// <param name="skillId">JSON中定义的ID，如 "Contract_A"</param>
        public bool IsSkillUnlocked(string skillId)
        {
            // 优先查询缓存
            if (_unlockedCache.Contains(skillId)) return true;
            
            // 如果缓存没有，且树存在，尝试从树中确认（双重保险）
            if (_runtimeTree != null)
            {
                var perk = _runtimeTree.Perks.FirstOrDefault(p => p.name == skillId);
                if (perk != null && perk.Unlocked)
                {
                    _unlockedCache.Add(skillId);
                    return true;
                }
            }
            return false;
        }

        // --- 核心构建流程 ---

        private IEnumerator BuildTreeRoutine()
        {
            yield return null; 

            // 1. 读取 JSON
            string jsonPath = Path.Combine(_modDir, "Skills", "MaidSkillTree.json");
            if (!File.Exists(jsonPath)) yield break;

            SkillTreeConfig config;
            try { config = JsonUtility.FromJson<SkillTreeConfig>(File.ReadAllText(jsonPath)); }
            catch { yield break; }

            // 2. 获取模板并克隆
            PerkTree template = PerkTreeManager.GetPerkTree("Skills");
            if (template == null) yield break;

            GameObject treeObj = UnityEngine.Object.Instantiate(template.gameObject);
            treeObj.name = $"MaidTree_{config.treeId}";
            UnityEngine.Object.DontDestroyOnLoad(treeObj);
            treeObj.SetActive(false);

            _runtimeTree = treeObj.GetComponent<PerkTree>();
            CleanTree(_runtimeTree, config.treeId);

            // 3. 构建节点
            Dictionary<string, Perk> perkMap = new Dictionary<string, Perk>();

            foreach (var nodeData in config.nodes)
            {
                perkMap[nodeData.id] = CreatePerkNode(nodeData);
            }

            // 4. 连接节点
            foreach (var nodeData in config.nodes)
            {
                if (nodeData.parentIDs == null) continue;
                foreach (string pId in nodeData.parentIDs)
                {
                    if (perkMap.ContainsKey(pId) && perkMap.ContainsKey(nodeData.id))
                    {
                        ConnectPerks(perkMap[pId], perkMap[nodeData.id]);
                    }
                }
            }

            // 5. 激活与注册
            treeObj.SetActive(true);
            if (PerkTreeManager.Instance != null && !PerkTreeManager.Instance.perkTrees.Contains(_runtimeTree))
            {
                PerkTreeManager.Instance.perkTrees.Add(_runtimeTree);
            }

            // 6. 加载存档并应用
            LoadProgress();
            
            // 7. 绑定到 NPC (你需要修改这里的 NPC 名字)
            _mod.StartCoroutine(BindToSkillMachineRoutine());

            CMDebug.Log($"[MaidSkill] 技能树构建完毕，节点数: {perkMap.Count}");
        }

        private Perk CreatePerkNode(SkillNodeData data)
        {
            GameObject go = new GameObject(data.id);
            go.transform.SetParent(_runtimeTree.transform);
            go.SetActive(false);

            Perk perk = go.AddComponent<Perk>();
            perk.name = data.id;

            // 添加我们自定义的观察者，替代 AutoSaveBehaviour
            go.AddComponent<MaidPerkObserver>();

            // 设置基础属性
            SetField(perk, "displayName", data.name);
            SetField(perk, "master", _runtimeTree);
            
            // 尝试设置描述 (如果 Perk 类里有这个字段)
            // 如果 Perk 类没有 description 字段，你可能需要依赖 Name 显示或自行实现 UI Tooltip 注入
            SetField(perk, "description", data.description); 
            // 或者有些版本是 "descriptionText"
            
            // 加载图标
            if (!string.IsNullOrEmpty(data.iconPath))
            {
                Sprite sp = LoadSprite(data.iconPath);
                if (sp != null) SetField(perk, "icon", sp);
            }

            // 设置消耗
            ConfigureCosts(perk, data.costs);

            // 添加到树的列表
            var perksList = GetField<System.Collections.IList>(_runtimeTree, "perks");
            perksList?.Add(perk);

            // 添加到图表
            AddToGraph(perk, new Vector2(data.posX, data.posY));

            go.SetActive(true);
            return perk;
        }

        // --- 存档逻辑 ---

        private string GetSavePath()
        {
            int slot = SavesSystem.CurrentSlot;
            string dir = Path.Combine(Application.dataPath, "Mods", "CombatMaid", "Saves");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"MaidSkills_Slot_{slot}.json");
        }

        public void SaveProgress()
        {
            if (_runtimeTree == null) return;

            SkillSaveData saveData = new SkillSaveData();
            
            foreach (var perk in _runtimeTree.Perks)
            {
                if (perk.Unlocked)
                {
                    saveData.unlockedSkillIDs.Add(perk.name);
                    _unlockedCache.Add(perk.name); // 更新缓存
                }
            }

            File.WriteAllText(GetSavePath(), JsonUtility.ToJson(saveData, true));
        }

        public void LoadProgress()
        {
            string path = GetSavePath();
            _unlockedCache.Clear();

            if (File.Exists(path) && _runtimeTree != null)
            {
                try
                {
                    var data = JsonUtility.FromJson<SkillSaveData>(File.ReadAllText(path));
                    var unlockProp = typeof(Perk).GetProperty("Unlocked");

                    foreach (var perk in _runtimeTree.Perks)
                    {
                        if (data.unlockedSkillIDs.Contains(perk.name))
                        {
                            unlockProp?.SetValue(perk, true);
                            _unlockedCache.Add(perk.name);
                        }
                    }
                }
                catch (Exception e) { CMDebug.LogError("存档读取失败: " + e.Message); }
            }
        }
    
        // 在 MaidSkillTreeManager 类中添加
        public PerkTree GetRuntimeTree()
        {
            return _runtimeTree;
        }
        
        // --- 辅助工具 (反射与图表) ---

        private void CleanTree(PerkTree tree, string newId)
        {
            foreach (Transform child in tree.transform) UnityEngine.Object.Destroy(child.gameObject);
            
            if (tree.RelationGraphOwner?.graph is PerkRelationGraph graph)
            {
                graph.allNodes.Clear();
                graph.GetGraphSource().connections.Clear();
                graph.UpdateGraph();
            }
            SetField(tree, "perkTreeID", newId);
            SetField(tree, "perks", new List<Perk>());
        }

        private void ConfigureCosts(Perk perk, List<CostData> costs)
        {
            if (costs == null || costs.Count == 0) return;
            var req = new PerkRequirement { level = 0, requireTime = TimeSpan.FromSeconds(0.5).Ticks };
            req.cost = new Cost { 
                items = costs.Select(c => new Cost.ItemEntry { id = c.itemId, amount = c.amount }).ToArray() 
            };
            SetField(perk, "requirement", req);
        }

        private void AddToGraph(Perk perk, Vector2 pos)
        {
            if (_runtimeTree?.RelationGraphOwner?.graph is PerkRelationGraph graph)
            {
                var node = graph.AddNode<PerkRelationNode>();
                if (node != null) { node.relatedNode = perk; node.cachedPosition = pos; }
                graph.UpdateGraph();
            }
        }

        private void ConnectPerks(Perk parent, Perk child)
        {
            // 逻辑连接
            var childReq = GetField<PerkRequirement>(child, "requirement");
            if (childReq != null)
            {
                // 注意：PerkRequirement 里的 perks 字段通常是 private list
                var listField = typeof(PerkRequirement).GetField("perks", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (listField != null)
                {
                    var list = listField.GetValue(childReq) as System.Collections.IList;
                    if (list == null) 
                    {
                        list = new List<Perk>();
                        listField.SetValue(childReq, list);
                    }
                    if (!list.Contains(parent)) list.Add(parent);
                }
            }

            // 视觉连接
            if (_runtimeTree?.RelationGraphOwner?.graph is PerkRelationGraph graph)
            {
                var pNode = graph.GetRelatedNode(parent);
                var cNode = graph.GetRelatedNode(child);
                if (pNode != null && cNode != null) graph.ConnectNodes(pNode, cNode, -1, -1);
            }
        }

        private Sprite LoadSprite(string path)
        {
            string full = Path.Combine(_modDir, path);
            if (!File.Exists(full)) return null;
            byte[] data = File.ReadAllBytes(full);
            Texture2D t = new Texture2D(2, 2);
            if (t.LoadImage(data)) return Sprite.Create(t, new Rect(0,0,t.width,t.height), new Vector2(0.5f,0.5f));
            return null;
        }

        private void SetField(object target, string name, object val)
        {
            if (target == null) return;
            var t = target.GetType();
            FieldInfo f = null;
            while(t!=null && f==null) { f=t.GetField(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public); t=t.BaseType; }
            f?.SetValue(target, val);
        }
        
        private T GetField<T>(object target, string name) where T : class
        {
            if (target == null) return null;
            var t = target.GetType();
            FieldInfo f = null;
            while(t!=null && f==null) { f=t.GetField(name, BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public); t=t.BaseType; }
            return f?.GetValue(target) as T;
        }

        private IEnumerator BindToSkillMachineRoutine()
        {
            // 技能机器通常是场景静态物体，加载后稍等一下就能找到
            yield return new WaitForSeconds(1.0f);

            GameObject machine = null;

            // 1. 尝试按路径查找 (参考代码的逻辑)
            GameObject buildings = GameObject.Find("Buildings");
            if (buildings != null)
            {
                var trans = buildings.transform.Find("SkillMachine");
                if (trans != null) machine = trans.gameObject;
            }

            // 2. 如果按路径没找到，尝试全局搜索 (备用方案)
            if (machine == null)
            {
                machine = GameObject.Find("SkillMachine");
            }

            // 3. 挂载交互脚本
            if (machine != null)
            {
                // 防止重复挂载
                if (machine.GetComponent<SkillTreeInteraction>() == null)
                {
                    var interaction = machine.AddComponent<SkillTreeInteraction>();
                    
                    // 设置交互参数
                    interaction.interactDistance = 4.0f; // 稍微大一点，方便操作
                    interaction.interactKey = KeyCode.G; // [重要] 使用 G 键，避免和原版机器的 F 键冲突
                    
                    CMDebug.Log("[MaidSkill] 已成功绑定到 SkillMachine (按 G 交互)");
                }
            }
            else
            {
                CMDebug.LogWarning("[MaidSkill] 未能在场景中找到 SkillMachine。");
            }
        }
    }
}