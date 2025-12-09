using UnityEngine;
using CombatMaid.Core.BuffsSystem;
using Duckov.Buffs;

namespace CombatMaid.Core.BuffsSystem.Effects
{
    /// <summary>
    /// 超级再生效果：每 0.5秒 恢复 10% 最大生命值
    /// </summary>
    public class MaidSuperRegenEffect : IMaidBuffEffect
    {
        public string BuffName => "MaidBuff_SuperRegen";
        public int BuffID => 888002;

        // 当 Buff 被施加时触发：挂载计时器组件
        public void OnBuffSetup(Buff buff, CharacterMainControl target)
        {
            if (target == null) return;

            // 挂载一个临时的 MonoBehaviour 来处理 Update 循环
            var ticker = target.gameObject.AddComponent<SuperRegenTicker>();
            ticker.Initialize(target);
            
            CMDebug.Log($"[{BuffName}] 已启动：挂载再生组件");
        }

        // 当 Buff 结束/被移除时触发：销毁计时器组件
        public void OnBuffDestroy(Buff buff, CharacterMainControl target)
        {
            if (target != null)
            {
                var ticker = target.GetComponent<SuperRegenTicker>();
                if (ticker != null)
                {
                    Object.Destroy(ticker);
                    CMDebug.Log($"[{BuffName}] 已结束：移除再生组件");
                }
            }
        }

        // =========================================================
        // 内部计时器组件 (负责每 0.5s 执行一次回血)
        // =========================================================
        private class SuperRegenTicker : MonoBehaviour
        {
            private CharacterMainControl _target;
            private float _timer;
            
            // 配置参数
            private const float Interval = 0.5f; // 时间间隔
            private const float HealPercent = 0.10f; // 每次回复 10%

            public void Initialize(CharacterMainControl target)
            {
                _target = target;
                _timer = 0f;
            }

            private void Update()
            {
                if (_target == null || _target.Health.IsDead) return;

                _timer += Time.deltaTime;
                if (_timer >= Interval)
                {
                    _timer = 0f; // 重置计时器
                    PerformHeal();
                }
            }

            private void PerformHeal()
            {
                if (_target.Health == null) return;

                // 计算回复量：最大生命值 * 10%
                float healAmount = _target.Health.MaxHealth * HealPercent;
                
                // 执行回复
                _target.Health.AddHealth(healAmount);
                
                // 飘字提示
                _target.PopText($"<color=#00FF00>+{healAmount:F0}</color>");
            }
        }
    }
}