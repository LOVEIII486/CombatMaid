namespace CombatMaid.Core.MaidEventSystem
{
    public interface IMaidEvent
    {
        string EventName { get; }
        bool IsDateActive();

        void OnMaidRegistered(MaidController maid, CharacterMainControl player);
    }
}