using CombatMaid.Core.Utilities;
using Duckov.Buffs;
using UnityEngine;

namespace CombatMaid.Core.BuffsSystem.Effects
{
    /// <summary>
    /// 衔玉祝福
    /// </summary>
    public class MaidJadeBlessingEffect : IMaidBuffEffect
    {
        public string BuffName => "MaidBuff_JadeBlessing";
        public int BuffID => 888004;
        public void OnBuffSetup(Buff buff, CharacterMainControl target) { }
        public void OnBuffDestroy(Buff buff, CharacterMainControl target) { }
    }

    /// <summary>
    /// 衔玉无敌
    /// </summary>
    public class MaidJadeInvincibleEffect : IMaidBuffEffect
    {
        public string BuffName => "MaidBuff_JadeInvincible";
        public int BuffID => 888005;

        public void OnBuffSetup(Buff buff, CharacterMainControl target)
        {
            if (target == null) return;
            target.Health.SetInvincible(true);
            var glow = target.gameObject.AddComponent<MaidGlowComponent>();
            glow.Play(new Color(1.0f, 0.8f, 0.0f) * 2.0f);
            //CMDebug.Log($"[{BuffName}] 启动：开启金色高亮无敌状态");
        }

        public void OnBuffDestroy(Buff buff, CharacterMainControl target)
        {
            if (target == null) return;
            target.Health.SetInvincible(false);
            var glow = target.GetComponent<MaidGlowComponent>();
            if (glow != null) glow.Stop(1.0f);
            //CMDebug.Log($"[{BuffName}] 结束：正在淡出高亮效果");
        }
    }
}