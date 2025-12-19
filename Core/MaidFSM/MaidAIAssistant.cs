using Duckov.Utilities;
using UnityEngine;

namespace CombatMaid.Core.MaidFSM
{
    /// <summary>
    /// 女仆AI助理：处理生存本能决策
    /// </summary>
    public class MaidAIAssistant
    {
        private readonly MaidController _owner;
        private AICharacterController _aiCtrl;
        private CharacterMainControl _character;

        private const float BackDetectionRadius = 8f;     // 背后检测半径
        private const float AdjustInterval = 0.4f;         // 决策频率
        private const float RetreatStepDistance = 3f;     // 每次拉扯向后移动的步长
        
        // 动态射程比例
        private const float RangeRetreatThresholdPercent = 0.35f; 
        
        private const float MinSafeLimit = 5f;   
        private const float MaxSafeLimit = 13f;  

        private float _nextAdjustTime;

        public MaidAIAssistant(MaidController owner)
        {
            _owner = owner;
            _aiCtrl = owner.GetComponentInChildren<AICharacterController>();
            _character = owner.MaidCharacter; 
        }

        public void OnTick()
        {
            if (_aiCtrl == null || _character == null || _character.Health.IsDead) return;

            UpdateBacksideDetection();

            if (Time.time > _nextAdjustTime)
            {
                UpdateDistanceLogic();
                _nextAdjustTime = Time.time + AdjustInterval;
            }
        }

        private void UpdateDistanceLogic()
        {
            if (_aiCtrl.searchedEnemy == null) return;

            // 获取动态安全距离
            float dynamicSafeDist = CalculateSafeDistance();
            
            // 计算当前距离
            float currentDist = Vector3.Distance(_character.transform.position, _aiCtrl.searchedEnemy.transform.position);

            if (currentDist < dynamicSafeDist)
            {
                // 计算远离方向
                Vector3 awayDir = (_character.transform.position - _aiCtrl.searchedEnemy.transform.position).normalized;
                Vector3 retreatPos = _character.transform.position + awayDir * RetreatStepDistance;

                // 注入移动指令给原生AI寻路
                _aiCtrl.MoveToPos(retreatPos);

                CMDebug.Log($"拉扯: 当前{currentDist:F1}m < 警戒线{dynamicSafeDist:F1}m");
            }
        }

        private float CalculateSafeDistance()
        {
            // 通过 CharacterMainControl 获取当前持有的枪械 Agent
            ItemAgent_Gun currentGun = _character.GetGun(); 
            
            if (currentGun != null)
            {
                // BulletDistance 是该武器的子弹飞行射程属性
                float weaponRange = currentGun.BulletDistance;
        
                // 计算红线距离：射程 * 配置比例
                float rawSafeDist = weaponRange * RangeRetreatThresholdPercent;
        
                // 最终限制在物理区间内
                float finalSafeDist = Mathf.Clamp(rawSafeDist, MinSafeLimit, MaxSafeLimit);

                //CMDebug.Log($"武器监测: {currentGun.Item.DisplayName} | 射程: {weaponRange}m | 比例计算值: {rawSafeDist:F1}m | 最终红线: {finalSafeDist:F1}m");

                return finalSafeDist;
            }

            // 若无武器（如空手或近战），回退到最小限制
            return MinSafeLimit;
        }

        private void UpdateBacksideDetection()
        {
            // 保护：确保AI控制器和感知字段可用
            if (_aiCtrl == null) return;

            if (_aiCtrl.searchedEnemy == null)
            {
                // 增加层级检查保护
                var mask = GameplayDataSettings.Layers.damageReceiverLayerMask;
                Collider[] cols = Physics.OverlapSphere(_character.transform.position, BackDetectionRadius, mask);

                foreach (var col in cols)
                {
                    if (col == null) continue;

                    var dr = col.GetComponent<DamageReceiver>();
                    // 修复点：必须同时检查 dr 和 dr.health，因为 dr.Team 依赖于 health 或初始化
                    if (dr != null && dr.health != null && !dr.health.IsDead)
                    {
                        if (Team.IsEnemy(_character.Team, dr.Team))
                        {
                            CMDebug.Log($"[AIAssistant] 察觉背后威胁: {dr.name}");
                            _aiCtrl.searchedEnemy = dr;
                            _aiCtrl.noticed = true;
                            break; 
                        }
                    }
                }
            }
        }
    }
}