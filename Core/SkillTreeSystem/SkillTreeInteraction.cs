using System.Reflection;
using UnityEngine;
using Duckov.PerkTrees;
using Duckov.UI;

namespace CombatMaid.Core.SkillTreeSystem
{
    public class SkillTreeInteraction : MonoBehaviour
    {
        public float interactDistance = 4.0f;
        public KeyCode interactKey = KeyCode.G; // 默认使用 G 键，避开原版 F 键

        private GUIStyle _style;
        private bool _showPrompt = false;

        private void OnGUI()
        {
            if (_showPrompt && CharacterMainControl.Main != null)
            {
                if (_style == null)
                {
                    _style = new GUIStyle(GUI.skin.label);
                    _style.fontSize = 20;
                    _style.fontStyle = FontStyle.Bold;
                    _style.normal.textColor = new Color(1f, 0.8f, 0.2f); // 金色文字
                    _style.alignment = TextAnchor.MiddleCenter;
                    // 给文字加个黑底背景，使其更清晰
                    Texture2D tex = new Texture2D(1, 1);
                    tex.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
                    tex.Apply();
                    _style.normal.background = tex;
                }

                // 在屏幕中下方显示提示 (比原版提示稍微靠上或靠下一点)
                float w = 300;
                float h = 40;
                Rect rect = new Rect((Screen.width - w) / 2, Screen.height * 0.75f, w, h);
                
                GUI.Label(rect, $"按 [{interactKey}] 管理女仆契约", _style);
            }
        }

        private void Update()
        {
            if (CharacterMainControl.Main == null) return;

            // 计算与玩家的距离
            float dist = Vector3.Distance(transform.position, CharacterMainControl.Main.transform.position);
            
            if (dist <= interactDistance)
            {
                _showPrompt = true;

                if (Input.GetKeyDown(interactKey))
                {
                    ToggleSkillTree();
                }
            }
            else
            {
                _showPrompt = false;
            }
        }

        private void ToggleSkillTree()
        {
            // 1. 获取运行时技能树
            var tree = MaidSkillTreeManager.Instance?.GetRuntimeTree(); 
            if (tree == null) return;

            // 2. 查找 UI 视图
            var uiView = FindObjectOfType<PerkTreeView>(true); 
            
            if (uiView != null)
            {
                if (!uiView.gameObject.activeSelf)
                {
                    // --- 打开逻辑 ---
                    
                    // A. 保存当前进度
                    MaidSkillTreeManager.Instance.SaveProgress(); 

                    // B. [关键修复] 将我们的树“注入”到 UI 中
                    // 由于不知道具体的字段名是 'tree', 'currentTree' 还是 'm_PerkTree'
                    // 我们使用反射尝试设置它。
                    InjectTreeIntoView(uiView, tree);
                    
                    // C. 确保树被管理器识别 (防止 UI 报错)
                    if (PerkTreeManager.Instance != null && !PerkTreeManager.Instance.perkTrees.Contains(tree))
                    {
                        PerkTreeManager.Instance.perkTrees.Add(tree);
                    }
                    
                    // D. 打开 UI
                    // Open 的参数是 ManagedUIElement (来源)，因为是从世界交互打开的，没有上级 UI，所以传 null
                    uiView.Open(null); 
                    uiView.gameObject.SetActive(true);
                }
                else
                {
                    // --- 关闭逻辑 ---
                    // 调用 Close 而不是直接 SetActive(false)，以触发 UI 的关闭动画或逻辑
                    uiView.Close(); 
                    MaidSkillTreeManager.Instance.SaveProgress();
                }
            }
        }

        // [新增] 反射注入辅助方法
        private void InjectTreeIntoView(PerkTreeView view, PerkTree tree)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var type = view.GetType();

            // 尝试查找可能的字段名 (通常是这几个之一)
            FieldInfo field = type.GetField("tree", flags) 
                           ?? type.GetField("m_Tree", flags) 
                           ?? type.GetField("perkTree", flags)
                           ?? type.GetField("currentTree", flags);

            if (field != null)
            {
                field.SetValue(view, tree);
                CMDebug.Log($"[MaidSkill] 成功将技能树注入 UI 字段: {field.Name}");
            }
            else
            {
                // 如果找不到字段，可能是通过 Property 设置的，或者基类字段
                CMDebug.LogWarning("[MaidSkill] 警告：无法通过反射找到 UI 的树引用字段，UI 可能显示为空白。");
            }
        }
    }
}