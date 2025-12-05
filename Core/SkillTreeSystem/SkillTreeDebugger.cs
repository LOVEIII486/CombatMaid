using System.Linq;
using HarmonyLib;
using UnityEngine;
using Duckov.PerkTrees;
using Duckov.PerkTrees.Behaviours;

namespace CombatMaid.Core.SkillTreeSystem
{
    /// <summary>
    /// 技能树调试助手
    /// 按 F10 键验证技能树状态
    /// </summary>
    public class SkillTreeDebugger : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                DiagnoseSkillTree();
            }
        }

        private void DiagnoseSkillTree()
        {
            CMDebug.LogInfo("========== [技能树诊断开始] ==========");

            // 1. 检查管理器实例
            if (SkillTreeManager.Instance == null)
            {
                CMDebug.LogError("✗ SkillTreeManager.Instance 为 null！");
                return;
            }
            CMDebug.LogInfo("✓ SkillTreeManager 已初始化");

            // 2. 检查技能树是否存在
            var tree = PerkTreeManager.GetPerkTree("MaidCombatSkills");
            if (tree == null)
            {
                CMDebug.LogWarning("✗ 找不到技能树 'MaidCombatSkills'");
                return;
            }
            CMDebug.LogInfo($"✓ 技能树已加载: {tree.name}");

            // 3. 遍历所有节点
            var perks = Traverse.Create(tree).Field("perks").GetValue<System.Collections.Generic.List<Perk>>();
            if (perks == null || perks.Count == 0)
            {
                CMDebug.LogWarning("✗ 技能树中没有节点！");
                return;
            }

            CMDebug.LogInfo($"✓ 找到 {perks.Count} 个技能节点");

            foreach (var perk in perks)
            {
                if (perk == null) continue;

                string id = perk.gameObject.name;
                // [修复] 使用公共属性而不是反射私有字段
                bool isUnlocked = perk.Unlocked;
                var behaviours = perk.GetComponents<PerkBehaviour>();

                CMDebug.LogInfo($"  - 节点: {id}");
                CMDebug.LogInfo($"    显示名: {perk.DisplayName}");
                CMDebug.LogInfo($"    解锁状态: {(isUnlocked ? "已解锁" : "未解锁")}");
                CMDebug.LogInfo($"    挂载组件数: {behaviours.Length}");

                foreach (var beh in behaviours)
                {
                    CMDebug.LogInfo($"      → {beh.GetType().Name}");
                    
                    // 检查绑定状态
                    var behPerk = Traverse.Create(beh).Field("perk").GetValue<Perk>();
                    if (behPerk == null)
                    {
                        CMDebug.LogWarning($"        ✗ 该组件未绑定到 Perk！");
                    }
                    else if (behPerk != perk)
                    {
                        CMDebug.LogWarning($"        ✗ 该组件绑定到了错误的 Perk！");
                    }
                    else
                    {
                        CMDebug.LogInfo($"        ✓ 绑定正常");
                    }
                }
            }

            // 4. 检查存档文件
            var saveData = SkillTreePersistence.Load();
            CMDebug.LogInfo($"✓ 存档中已解锁节点数: {saveData.UnlockedNodeIDs.Count}");
            foreach (var unlockedId in saveData.UnlockedNodeIDs)
            {
                CMDebug.LogInfo($"  - {unlockedId}");
            }

            CMDebug.LogInfo("========== [诊断结束] ==========");
        }
    }
}