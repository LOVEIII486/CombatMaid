using UnityEngine;

namespace CombatMaid.Core.MaidFSM.States
{
    public class State_PassiveFollow : MaidStateBase
    {
        public override void Enter()
        {
            SetNativeBrainActive(true);
            if (Controller.AI != null)
            {
                Controller.SetSensorySuppression(true);
                Controller.AI.leader = Controller.MainOwner;
                Controller.AI.PutBackWeapon();
            }

            Controller.MaidCharacter?.PopText("和平跟随");
        }

        public override void Update()
        {
            if (Controller.MainOwner == null || Controller.AI == null) return;

            // if (Controller.AI.searchedEnemy != null || Controller.AI.noticed)
            // {
            //     Controller.ClearImmediateThreats();
            // }

            float dist = Vector3.Distance(Controller.transform.position, Controller.MainOwner.transform.position);
            if (dist > Controller.ForceFollowDistance) 
            {
                // 切换到强制跟随，并指定“完成后回到被动模式”
                Machine.ChangeState<State_ForceFollow>(state => 
                {
                    state.NextStateAction = () => Machine.ChangeState<State_PassiveFollow>();
                });
                return;
            }

            if (dist > Controller.TeleportDistance)
            {
                TeleportToOwner();
            }
        }

        private void TeleportToOwner()
        {
            Controller.transform.position = Controller.MainOwner.transform.position;
            if (Controller.AI.transform.parent != Controller.transform)
            {
                Controller.AI.transform.position = Controller.MainOwner.transform.position;
            }
            Controller.MaidCharacter?.PopText("跟丢传送");
            Controller.AI.StopMove();
        }
        
        public override void Exit()
        {
            if (Controller.AI != null)
            {
                Controller.SetSensorySuppression(false);
                Controller.AI.StopMove();
            }
        }
    }
}