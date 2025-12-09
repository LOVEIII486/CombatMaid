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
        public virtual float Cooldown => 10.0f;     
        public virtual bool CanUseWhileMoving => true; 
        
        // 该技能是否受公共冷却影响？
        public virtual bool RespectGlobalCooldown => true;
        
        // 该技能释放后触发的公共冷却时间 (默认1秒)
        public virtual float TriggerGCDDuration => 1.0f;

        protected float _cooldownTimer = 0f;

        public virtual void Initialize(MaidController controller)
        {
            Controller = controller;
            _cooldownTimer = UnityEngine.Random.Range(0f, 5.0f);;
        }

        public void OnUpdate(float deltaTime)
        {
            if (Controller == null || Owner == null || Owner.Health.IsDead) return;

            // 1. 冷却计算
            if (_cooldownTimer > 0)
            {
                _cooldownTimer -= deltaTime;
                return; // 自身冷却未好，直接返回
            }
            
            // 2. 检查公共冷却 (如果技能需要遵循GCD)
            if (RespectGlobalCooldown && Controller.SkillSystem.IsGlobalCooldownActive)
            {
                return; // 系统忙碌，跳过
            }

            // 3. 检查是否满足释放条件
            if (CheckTriggerCondition())
            {
                // 4. 执行技能
                if (TryExecute())
                {
                    _cooldownTimer = Cooldown; // 重置自身冷却
                    
                    // [新增] 触发系统的公共冷却
                    if (RespectGlobalCooldown)
                    {
                        Controller.SkillSystem.TriggerGlobalCooldown(TriggerGCDDuration);
                    }
                    
                    OnSkillExecuted();
                }
            }
        }

        public virtual void OnCleanup() { }
        
        protected abstract bool CheckTriggerCondition();

        protected abstract bool TryExecute();

        protected virtual void OnSkillExecuted() 
        {
            CMDebug.Log($"{SkillName} 释放成功");
        }
    }
}