using System.Collections.Generic;
using UnityEngine;
using System.Linq; // [新增] 引用 Linq

namespace CombatMaid.Core.MaidSkillSystem
{
    /// <summary>
    /// 女仆技能管理器
    /// </summary>
    public class MaidSkillComponent : MonoBehaviour
    {
        private MaidController _controller;
        private List<IMaidSkill> _skills = new List<IMaidSkill>();
        private bool _isInitialized = false;
        public int SkillCount => _skills.Count;

        public void Initialize(MaidController controller)
        {
            _controller = controller;
            _isInitialized = true;
        }

        /// <summary>
        /// 动态添加技能
        /// </summary>
        public void AddSkill(IMaidSkill skill)
        {
            if (skill == null || !_isInitialized) return;

            foreach (var existing in _skills)
            {
                if (existing.SkillName == skill.SkillName)
                {
                    CMDebug.LogWarning($"技能 {skill.SkillName} 已存在，跳过添加。");
                    return;
                }
            }
            
            skill.Initialize(_controller);
            _skills.Add(skill);
            CMDebug.Log($"已装载技能: {skill.SkillName}");
        }

        /// <summary>
        /// [新增] 按类型查找技能实例
        /// </summary>
        /// <typeparam name="T">具体的技能类型 (如 Skill_SelfHeal)</typeparam>
        /// <returns>找到的技能实例，没找到返回 null</returns>
        public T GetSkill<T>() where T : class, IMaidSkill
        {
            // 遍历查找第一个匹配该类型的技能
            foreach (var skill in _skills)
            {
                if (skill is T targetSkill)
                {
                    return targetSkill;
                }
            }
            return null;
        }

        private void Update()
        {
            if (!_isInitialized || _skills.Count == 0) return;

            float dt = Time.deltaTime;
            // 倒序遍历以防移除安全
            for (int i = _skills.Count - 1; i >= 0; i--)
            {
                _skills[i].OnUpdate(dt);
            }
        }

        private void OnDestroy()
        {
            foreach (var skill in _skills)
            {
                skill.OnCleanup();
            }
            _skills.Clear();
        }
    }
}