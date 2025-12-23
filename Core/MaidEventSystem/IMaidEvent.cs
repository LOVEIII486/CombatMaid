namespace CombatMaid.Core.MaidEventSystem
{
    public interface IMaidEvent
    {
        // 注册入队时触发
        void OnMaidRegistered(MaidController maid, CharacterMainControl player);
    }
}