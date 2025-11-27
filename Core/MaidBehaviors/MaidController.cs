using UnityEngine;
using UnityEngine.AI;
using Duckov.Modding;

namespace CombatMaid.MaidBehaviors
{
    /// <summary>
    /// 女仆行为控制器
    /// 挂载在每个 AI 实体上，负责处理移动、战斗和特殊指令
    /// </summary>
    public class MaidController : MonoBehaviour
    {
        private AICharacterController _ai;
        private CharacterMainControl _player;
        private NavMeshAgent _agent;
        private Core.MaidProfile _profile;

        // 状态标记：当为 true 时，Harmony 补丁将拦截游戏原生 AI 的干扰
        public bool IsOverrideActive { get; private set; } = false;
        private float _overrideTimer = 0f;

        public void Initialize(Core.MaidProfile profile, CharacterMainControl player)
        {
            _ai = GetComponent<AICharacterController>();
            _agent = GetComponent<NavMeshAgent>();
            _player = player;
            _profile = profile;

            // 基础设置
            if (_ai != null) _ai.leader = player;
            if (_agent != null) _agent.autoRepath = false; // 禁止自动重算路径，提高控制稳定性
        }

        private void Update()
        {
            // 处理强制指令的计时器 (如强制移动)
            if (IsOverrideActive)
            {
                _overrideTimer -= Time.deltaTime;
                if (_overrideTimer <= 0)
                {
                    StopOverride(); // 时间到，恢复自由跟随
                }
            }
            
            // TODO: 在这里可以添加其他行为检测，例如检测附近是否有可拾取的战利品
        }

        /// <summary>
        /// 执行强制移动指令
        /// </summary>
        public void ForceMoveTo(Vector3 position)
        {
            if (_ai == null || _agent == null) return;

            // 1. 激活覆盖模式，屏蔽原生 AI
            IsOverrideActive = true;
            _overrideTimer = 5.0f; // 给予 5 秒时间执行移动

            // 2. 停止当前动作
            _ai.StopMove();
            _agent.isStopped = false;

            // 3. 执行移动
            _ai.MoveToPos(position);
            _agent.SetDestination(position); // 双重保险

            // 4. 反馈
            _ai.CharacterMainControl.PopText("收到！", 1f);
        }

        private void StopOverride()
        {
            IsOverrideActive = false;
            // 可以在这里让女仆说句话，比如 "就位"
        }
    }
}