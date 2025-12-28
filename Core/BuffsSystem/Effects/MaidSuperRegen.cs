using UnityEngine;
using Duckov.Buffs;

namespace CombatMaid.Core.BuffsSystem.Effects
{
    /// <summary>
    /// 超级再生效果：每 0.2秒 恢复 6% 最大生命值
    /// </summary>
    public class MaidSuperRegenEffect : IMaidBuffEffect
    {
        public string BuffName => "MaidBuff_SuperRegen";
        public int BuffID => 888002;

        public void OnBuffSetup(Buff buff, CharacterMainControl target)
        {
            if (target == null) return;

            var ticker = target.gameObject.AddComponent<SuperRegenTicker>();
            ticker.Initialize(target);
            
            CMDebug.Log($"[{BuffName}] 灵力共鸣已建立：挂载再生频率稳定器");
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl target)
        {
            if (target != null)
            {
                var ticker = target.GetComponent<SuperRegenTicker>();
                if (ticker != null)
                {
                    Object.Destroy(ticker);
                    CMDebug.Log($"[{BuffName}] 灵力连接已断开：移除再生组件");
                }
            }
        }

        private class SuperRegenTicker : MonoBehaviour
        {
            private CharacterMainControl _target;
            private float _healTimer;
            private float _uiTimer;
            private float _accumulatedHeal; // 累积治疗量
            
            private const float HealInterval = 0.2f;    // 治疗逻辑间隔
            private const float DisplayInterval = 1.0f; // UI 显示间隔
            private const float HealPercent = 0.06f;    // 6% 应为 0.06

            public void Initialize(CharacterMainControl target)
            {
                _target = target;
                _healTimer = 0f;
                _uiTimer = 0f;
                _accumulatedHeal = 0f;
            }

            private void Update()
            {
                if (_target == null || _target.Health == null || _target.Health.IsDead) return;

                float deltaTime = Time.deltaTime;
                _healTimer += deltaTime;
                _uiTimer += deltaTime;

                // 1. 核心逻辑层：保持高频治疗，但只累加数值
                if (_healTimer >= HealInterval)
                {
                    _healTimer -= HealInterval;
                    PerformHealLogic();
                }

                // 2. 表现层：低频刷新 PopText
                if (_uiTimer >= DisplayInterval)
                {
                    _uiTimer -= DisplayInterval;
                    ShowAccumulatedHeal();
                }
            }

            private void PerformHealLogic()
            {
                float healAmount = _target.Health.MaxHealth * HealPercent;
                _target.Health.AddHealth(healAmount);
                _accumulatedHeal += healAmount;
            }

            private void ShowAccumulatedHeal()
            {
                if (_accumulatedHeal > 0.1f)
                {
                    _target.PopText($"<color=#00FF00>+{_accumulatedHeal:F0}</color>");
                    _accumulatedHeal = 0f;
                }
            }
        }
    }
}