namespace CombatMaid.Core.MaidEventSystem
{
    public interface IMaidEvent
    {
        bool IsDateActive();

        void OnMaidRegistered(MaidController maid, CharacterMainControl player);
    }
}