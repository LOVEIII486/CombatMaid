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


        private class SuperRegenTicker : MonoBehaviour
        {
            private CharacterMainControl _target;
            private float _timer;
            
            private const float Interval = 0.2f; // 时间间隔
            private const float HealPercent = 0.6f; // 每次回复 6%

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
                float healAmount = _target.Health.MaxHealth * HealPercent;
                _target.Health.AddHealth(healAmount);
                _target.PopText($"<color=#00FF00>+{healAmount:F0}</color>");
            }
        }
    }
}