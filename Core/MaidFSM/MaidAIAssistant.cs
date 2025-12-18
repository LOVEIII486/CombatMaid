using Duckov.Utilities;
using UnityEngine;

namespace CombatMaid.Core.MaidFSM
{
    /// <summary>
    /// 女仆AI助理：处理跨状态的本能决策，如距离把控和背后感官增强
    /// 生命周期与 MaidController 绑定
    /// </summary>
    public class MaidAIAssistant
    {
        private readonly MaidController _owner;
        private AICharacterController _aiCtrl;
        private CharacterMainControl _character;

        // 策略参数（可根据需要暴露给配置）
        private const float BACK_DETECTION_RADIUS = 8f; 
        private const float MIN_SAFE_DISTANCE = 8f;
        private const float ADJUST_INTERVAL = 0.4f;
        private float _nextAdjustTime;

        public MaidAIAssistant(MaidController owner)
        {
            _owner = owner;
            // 在初始化时缓存引用
            _aiCtrl = owner.GetComponentInChildren<AICharacterController>();
            _character = owner.MaidCharacter; 
        }

        /// <summary>
        /// 每帧更新逻辑
        /// </summary>
        public void OnTick()
        {
            if (_aiCtrl == null || _character == null || _character.Health.IsDead) return;

            // 1. 本能：背后感官注入
            UpdateBacksideDetection();

            // 2. 策略：距离拉扯（仅在有目标时生效）
            if (Time.time > _nextAdjustTime)
            {
                UpdateDistanceLogic();
                _nextAdjustTime = Time.time + ADJUST_INTERVAL;
            }
        }

        private void UpdateBacksideDetection()
        {
            // 如果原生AI还没发现敌人，助理帮它“回头”
            if (_aiCtrl.searchedEnemy == null)
            {
                Collider[] cols = Physics.OverlapSphere(_character.transform.position, BACK_DETECTION_RADIUS, 
                    GameplayDataSettings.Layers.damageReceiverLayerMask);

                foreach (var col in cols)
                {
                    var dr = col.GetComponent<DamageReceiver>();
                    if (dr != null && !dr.health.IsDead && Team.IsEnemy(_character.Team, dr.Team))
                    {
                        CMDebug.Log($"女仆 {_owner.name} 察觉到背后敌人");
                        _aiCtrl.searchedEnemy = dr;
                        _aiCtrl.noticed = true;
                        break; 
                    }
                }
            }
        }

        private void UpdateDistanceLogic()
        {
            if (_aiCtrl.searchedEnemy == null) return;

            float dist = Vector3.Distance(_character.transform.position, _aiCtrl.searchedEnemy.transform.position);
            if (dist < MIN_SAFE_DISTANCE)
            {
                // 计算远离目标的点
                Vector3 awayDir = (_character.transform.position - _aiCtrl.searchedEnemy.transform.position).normalized;
                Vector3 retreatPos = _character.transform.position + awayDir * 4f;

                // 强制介入原生AI移动系统
                _aiCtrl.MoveToPos(retreatPos);
                CMDebug.Log("强制介入原生AI移动，远离敌人");
            }
        }
    }
}