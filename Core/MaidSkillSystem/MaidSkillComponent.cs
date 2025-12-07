using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
        
        private float _globalCooldownTimer = 0f;
        
        private const float DefaultGCD = 1.0f;

        public int SkillCount => _skills.Count;

        public bool IsGlobalCooldownActive => _globalCooldownTimer > 0;

        public void Initialize(MaidController controller)
        {
            _controller = controller;
            _isInitialized = true;
        }

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

        public T GetSkill<T>() where T : class, IMaidSkill
        {
            foreach (var skill in _skills)
            {
                if (skill is T targetSkill)
                {
                    return targetSkill;
                }
            }
            return null;
        }

        public void TriggerGlobalCooldown(float duration = -1f)
        {
            float time = duration > 0 ? duration : DefaultGCD;
            
            if (time > _globalCooldownTimer)
            {
                _globalCooldownTimer = time;
            }
        }

        private void Update()
        {
            if (!_isInitialized) return;

            float dt = Time.deltaTime;

            // 更新公共冷却
            if (_globalCooldownTimer > 0)
            {
                _globalCooldownTimer -= dt;
            }

            if (_skills.Count == 0) return;

            // 倒序遍历
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