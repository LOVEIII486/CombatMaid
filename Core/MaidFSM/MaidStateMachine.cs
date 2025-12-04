using System;
using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.MaidFSM
{
    public class MaidStateMachine
    {
        private MaidController _owner;
        private Dictionary<Type, MaidStateBase> _states = new Dictionary<Type, MaidStateBase>();
        public MaidStateBase CurrentState { get; private set; }

        public MaidStateMachine(MaidController owner)
        {
            _owner = owner;
        }

        // 注册状态
        public void AddState(MaidStateBase state)
        {
            state.Initialize(_owner, this);
            _states[state.GetType()] = state;
        }

        // 切换状态
        public void ChangeState<T>() where T : MaidStateBase
        {
            ChangeState(typeof(T));
        }
        
        // 带参数切换
        public void ChangeState<T>(Action<T> initializer) where T : MaidStateBase
        {
            if (_states.TryGetValue(typeof(T), out var state))
            {
                initializer?.Invoke(state as T);
                ChangeState(typeof(T));
            }
        }

        private void ChangeState(Type type)
        {
            if (CurrentState != null && CurrentState.GetType() == type) return;

            if (_states.TryGetValue(type, out var newState))
            {
                if (CurrentState != null)
                {
                    CurrentState.Exit();
                    CMDebug.Log($"退出状态: {CurrentState.GetType().Name}");
                }

                CurrentState = newState;
                CurrentState.Enter();
                CMDebug.Log($"进入状态: {CurrentState.GetType().Name}");
            }
            else
            {
                CMDebug.LogError($"试图切换到未注册的状态: {type.Name}");
            }
        }

        public void Update()
        {
            CurrentState?.Update();
        }
    }
}