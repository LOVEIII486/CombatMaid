using Duckov.Buffs;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// 女仆 Buff 效果接口
    /// </summary>
    public interface IMaidBuffEffect
    {
        string BuffName { get; }
        int BuffID { get; }
        void OnBuffSetup(Buff buff, CharacterMainControl target);
        void OnBuffDestroy(Buff buff, CharacterMainControl target);
    }
}