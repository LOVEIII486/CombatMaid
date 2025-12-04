using Duckov.Buffs;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// 女仆 Buff 效果接口
    /// </summary>
    public interface IMaidBuffEffect
    {
        // Buff 的唯一标识符
        string BuffName { get; }

        // 当 Buff 被施加时触发
        void OnBuffSetup(Buff buff, CharacterMainControl target);

        // 当 Buff 结束/被移除时触发
        void OnBuffDestroy(Buff buff, CharacterMainControl target);
    }
}