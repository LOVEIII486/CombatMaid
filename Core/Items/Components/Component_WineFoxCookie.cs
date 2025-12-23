using ItemStatsSystem;
using CombatMaid.Core.BuffsSystem;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 酒狐的心意曲奇：恢复饱食度 + 满血 + 临时生命
    /// </summary>
    public class Component_WineFoxCookie : UsageBehavior
    {
        private const float EnergyAmount = 30f;
        private const int CookieBuffID = 888003;
        private const float CookieBuffDuration = 300f;

        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl character)) return false;
            return !character.Health.IsDead;
        }

        protected override void OnUse(Item item, object user)
        {
            var character = user as CharacterMainControl;
            if (character == null) return;

            character.AddEnergy(EnergyAmount);
            
            var config = new MaidBuffFactory.BuffConfig("MaidBuff_WineFoxCookie", CookieBuffID, CookieBuffDuration);
            var buffPfb = MaidBuffFactory.GetOrCreateSharedBuff(config);
            if (buffPfb != null)
            {
                character.AddBuff(buffPfb, character);
            }
        }
    }
}