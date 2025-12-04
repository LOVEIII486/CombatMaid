namespace CombatMaid.Core.MaidFSM.States
{
    /// <summary>
    /// 自主模式：把控制权完全交给原生行为树
    /// </summary>
    public class State_Autonomous : MaidStateBase
    {
        public override void Enter()
        {
            // 恢复原生AI
            SetNativeBrainActive(true);
            
            if (Controller.AI != null && Controller.AI.CharacterMainControl != null)
            {
                // 根据需要设置被动巡逻点
                if (Controller.MainOwner != null)
                {
                    Controller.AI.patrolPosition = Controller.MainOwner.transform.position;
                }
            }
        }

        public override void Update()
        {
            // 仍然需要检查是否离主人太远
            // 如果太远，则请求切换到强制跟随状态
            if (Controller.IsTooFarFromOwner())
            {
                Machine.ChangeState<State_ForceFollow>();
            }
            
            // 持续更新巡逻点到主人身边
            if (Controller.AI != null && Controller.MainOwner != null)
            {
                Controller.AI.patrolPosition = Controller.MainOwner.transform.position;
            }
        }
    }
}