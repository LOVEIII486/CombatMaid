using CombatMaid.Core.BuffsSystem;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 酒狐的衔玉酒瓶：施加致命保护祝福
    /// </summary>
    public class Component_JadeFlask : UsageBehavior
    {
        private const int BlessingBuffID = 888004; // 祝福 Buff ID
        private const float BlessingDuration = 180f;
        private const float WaterAmount = 30f;

        public override bool CanBeUsed(Item item, object user)
        {
            if (!(user is CharacterMainControl character)) return false;
            return !character.Health.IsDead;
        }

        protected override void OnUse(Item item, object user)
        {
            var character = user as CharacterMainControl;
            if (character == null) return;

            var config = new MaidBuffFactory.BuffConfig("MaidBuff_JadeBlessing", BlessingBuffID, BlessingDuration);
            var buffPfb = MaidBuffFactory.GetOrCreateSharedBuff(config);
            if (buffPfb != null)
            {
                character.AddBuff(buffPfb, character);
            }
            
            character.AddWater(WaterAmount);
            //CMDebug.Log($"[衔玉酒瓶] 使用者 {character.name} 已获得衔玉之护。");
        }
    }
}