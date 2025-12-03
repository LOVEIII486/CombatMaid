using UnityEngine;
using Duckov.Modding;
using NodeCanvas.Framework; // 必须引用：用于控制大脑开关

namespace CombatMaid.Core.MaidBehaviors
{
    /// <summary>
    /// 女仆移动模块 (最终傀儡师版：不失忆、不抽搐)
    /// </summary>
    public class MaidMovement : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidMovement]";
        
        private MaidController _controller;
        private GraphOwner _brain; // 原版 AI 大脑引用
        
        // 状态标记
        public bool IsActive { get; private set; } = false;
        
        // 计时器
        private float _failsafeTimer = 0f;
        private float _forceLockUntilTime = 0f;

        public void Initialize(MaidController controller)
        {
            _controller = controller;
            if (_controller.AI != null)
            {
                // 获取 AI 的决策大脑组件
                _brain = _controller.AI.GetComponent<GraphOwner>();
            }
        }

        public void OnUpdate()
        {
            if (!IsActive) return;

            _failsafeTimer -= Time.deltaTime;

            // 1. 刚开始给一点时间缓冲，防止刚下令就判断到达
            if (Time.time < _forceLockUntilTime) return;

            // 2. 判断是否结束
            if (HasArrived() || _failsafeTimer <= 0)
            {
                if (_failsafeTimer <= 0) 
                    Debug.LogWarning($"{LogTag} 移动超时，强制恢复自主模式。");
                else 
                    Debug.Log($"{LogTag} 到达目的地，恢复自主模式。");
                
                StopMove();
            }
        }

        public void MoveTo(Vector3 position)
        {
            if (_controller == null || _controller.AI == null) return;

            // 1. 【核心】暂停大脑 (冻结决策，保留仇恨状态)
            // 这样 AI 就不会“想起”要去攻击或者找掩体，而是保持当前的仇恨列表静止
            if (_brain != null && _brain.isRunning)
            {
                _brain.PauseBehaviour();
            }

            IsActive = true;
            _failsafeTimer = 15.0f; 
            _forceLockUntilTime = Time.time + 0.5f;

            // 2. 【核心】废弃 SetPeaceMode
            // _controller.SetPeaceMode(true); <--- 删掉这行！不要调用它！

            // 3. 【外科手术】只清除“瞄准锁定”，防止身体扭曲
            // searchedEnemy (仇恨目标) 保持不变！
            _controller.AI.aimTarget = null; 
            
            // 4. 执行底层移动
            // 先 Stop 此时可能存在的原版寻路
            _controller.AI.StopMove();
            // 再执行我们的指令
            _controller.AI.MoveToPos(position);

            if (_controller.AI.CharacterMainControl != null)
            {
                _controller.AI.CharacterMainControl.PopText("战术机动...");
            }
            
            // Debug.Log($"{LogTag} 暂停大脑 -> 移动中");
        }

        public void StopMove()
        {
            if (!IsActive) return;

            IsActive = false; 
            
            if (_controller != null && _controller.AI != null)
            {
                var ai = _controller.AI;
                
                // 1. 停止物理移动
                ai.StopMove();
                
                // 2. 【核心】基于距离的“战术遗忘”
                // 只有当敌人距离过远时，才重置仇恨。这解决了“跑回去”的问题，
                // 同时保留了“短距离战术移动后继续压制”的能力。
                if (ai.searchedEnemy != null)
                {
                    float distToEnemy = Vector3.Distance(ai.transform.position, ai.searchedEnemy.transform.position);
                    
                    // 阈值设定：比如 25米 (原版 sightDistance 通常是 20-30)
                    // 如果移动后敌人太远，就认为我们已经成功“脱离接触”
                    if (distToEnemy > 25.0f)
                    {
                        // 清除物理层面的锁定
                        ai.searchedEnemy = null;
                        ai.aimTarget = null;
                        
                        // 清除感知层面的锁定
                        ai.noticed = false; 
                        
                        // [可选] 如果你想让她彻底冷静下来，也可以重置 alert
                        // ai.alert = false; 
                        
                        if (ai.CharacterMainControl != null)
                        {
                            ai.CharacterMainControl.PopText("脱离接触-重新索敌");
                        }
                    }
                }
            }
            
            // 3. 唤醒大脑
            if (_brain != null && _brain.isPaused)
            {
                // GraphOwner 的 StartBehaviour 在暂停状态下相当于 Resume
                _brain.StartBehaviour();
                
                // [进阶] 如果你发现仅仅清空变量还不够（比如行为树内部卡在“追击”节点），
                // 可以强制发送一个事件或者重启行为树（慎用，可能会导致T-Pose一瞬间）
                // _brain.RestartBehaviour(); // 除非万不得已，否则不要用这个
            }
        }

        private bool HasArrived()
        {
            if (_controller == null || _controller.AI == null) return true;
            var ai = _controller.AI;
            
            if (ai.WaitingForPathResult()) return false;
            if (ai.ReachedEndOfPath()) return true;
            // 如果AI停下来了且不在计算路径，也算到达
            if (!ai.IsMoving() && ai.HasPath()) return true;

            return false;
        }
    }
}