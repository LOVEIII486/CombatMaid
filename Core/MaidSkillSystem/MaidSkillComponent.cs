using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.MaidSkillSystem
{
    /// <summary>
    /// 女仆技能管理器 (挂载在 GameObject 上)
    /// </summary>
    public class MaidSkillComponent : MonoBehaviour
    {
        private MaidController _controller;
        private List<IMaidSkill> _skills = new List<IMaidSkill>();
        private bool _isInitialized = false;

        // [修复] 添加 SkillCount 属性供外部访问
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
            
            skill.Initialize(_controller);
            _skills.Add(skill);
            Debug.Log($"[MaidSkill] 已装载技能: {skill.SkillName}");
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