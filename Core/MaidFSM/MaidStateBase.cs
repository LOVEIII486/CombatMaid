using UnityEngine;
using NodeCanvas.Framework; // 用于控制大脑

namespace CombatMaid.Core.MaidFSM
{
    public abstract class MaidStateBase
    {
        protected MaidController Controller;
        protected MaidStateMachine Machine;
        protected GraphOwner Brain; // 原生AI大脑引用

        public void Initialize(MaidController controller, MaidStateMachine machine)
        {
            Controller = controller;
            Machine = machine;
            if (Controller.AI != null)
            {
                Brain = Controller.AI.GetComponent<GraphOwner>();
            }
        }

        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Update() { }

        // 辅助方法：控制原生大脑开关
        protected void SetNativeBrainActive(bool active)
        {
            if (Brain == null) return;
            
            if (active)
            {
                if (Brain.isPaused) Brain.StartBehaviour();
            }
            else
            {
                if (Brain.isRunning) Brain.PauseBehaviour();
            }
        }
    }
}