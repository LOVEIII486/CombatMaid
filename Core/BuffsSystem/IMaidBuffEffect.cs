using Duckov.Buffs;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// 女仆 Buff 效果接口
    /// </summary>
    public interface IMaidBuffEffect
    {
        // Buff 的唯一标识名称
        string BuffName { get; }
        
        // Buff 的唯一数字 ID
        int BuffID { get; }

        void OnBuffSetup(Buff buff, CharacterMainControl target);
        void OnBuffDestroy(Buff buff, CharacterMainControl target);
    }
}