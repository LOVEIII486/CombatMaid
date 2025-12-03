using UnityEngine;

namespace CombatMaid.Core.MaidSkillSystem
{
    public interface IMaidSkill
    {
        string SkillName { get; }
        void Initialize(MaidController controller);
        void OnUpdate(float deltaTime);
        void OnCleanup();
    }

    /// <summary>
    /// 女仆技能基类：处理冷却和触发检查
    /// </summary>
    public abstract class MaidSkillBase : IMaidSkill
    {
        protected MaidController Controller;
        protected CharacterMainControl Owner => Controller?.MaidCharacter;

        // 技能配置
        public abstract string SkillName { get; }
        public virtual float Cooldown => 10.0f;     // 默认冷却
        public virtual bool CanUseWhileMoving => true; // 是否允许移动时释放

        // 运行时状态
        protected float _cooldownTimer = 0f;

        public virtual void Initialize(MaidController controller)
        {
            Controller = controller;
            _cooldownTimer = Cooldown; // 初始冷却，防止生成瞬间全扔出去
        }

        public void OnUpdate(float deltaTime)
        {
            if (Controller == null || Owner == null || Owner.Health.IsDead) return;

            // 1. 冷却计算
            if (_cooldownTimer > 0)
            {
                _cooldownTimer -= deltaTime;
                return;
            }

            // 2. 检查是否满足释放条件
            if (CheckTriggerCondition())
            {
                // 3. 执行技能
                if (TryExecute())
                {
                    _cooldownTimer = Cooldown; // 重置冷却
                    OnSkillExecuted();
                }
            }
        }

        public virtual void OnCleanup() { }

        // --- 子类必须实现 ---
        
        /// <summary>
        /// 检查是否应该释放技能 (例如：血量低于X，周围有敌人)
        /// </summary>
        protected abstract bool CheckTriggerCondition();

        /// <summary>
        /// 执行技能的具体逻辑
        /// </summary>
        protected abstract bool TryExecute();

        protected virtual void OnSkillExecuted() 
        {
            // Debug.Log($"[Skill] {SkillName} 释放成功");
        }
    }
}