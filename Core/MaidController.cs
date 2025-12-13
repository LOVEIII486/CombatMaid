using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.MaidFSM;
using CombatMaid.Core.MaidFSM.States;
using CombatMaid.Core.MaidSkillSystem;
using CombatMaid.Core.MaidSkillSystem.Skills;
using CombatMaid.Core.SkillTreeSystem;
using Duckov.Scenes;
using ItemStatsSystem;
using UnityEngine.SceneManagement;

namespace CombatMaid.Core
{
    /// <summary>
    /// 女仆核心控制器：负责组件组装、状态机驱动及对外接口
    /// </summary>
    public class MaidController : MonoBehaviour
    {
        private static readonly Dictionary<AICharacterController, MaidController> _maidRegistry 
            = new Dictionary<AICharacterController, MaidController>();

        public List<Item> LootHistory { get; private set; } = new List<Item>();
            
        public static MaidController GetMaid(AICharacterController ai)
        {
            if (ai == null) return null;
            return _maidRegistry.TryGetValue(ai, out var maid) ? maid : null;
        }
        
        #region 核心

        public AICharacterController AI { get; private set; }
        public CharacterMainControl MaidCharacter => AI != null ? AI.CharacterMainControl : null;
        public CharacterMainControl MainOwner { get; private set; }
        
        public MaidStateMachine StateMachine { get; private set; }
        public MaidSkillComponent SkillSystem { get; private set; }
        
        //缓存引用
        private CharacterMainControl _cachedCharacter;

        #endregion

        #region 默认设置
        
        public float HoldMaxDistance = 25.0f;            // 驻守模式最大宽容距离
        public float TeleportDistance = 30.0f;           // 强制传送距离
        public float TeleportTimeout = 5.0f;             // 强制跟随卡死超时时间
        
        public float ForceFollowDistance = 20.0f;        // 强制跟随触发距离
        public float SafeDistanceToResumeCombat = 5.0f; // 恢复自主战斗的安全距离

        /// <summary>
        /// 是否处于非原版接管状态
        /// </summary>
        public bool IsOverrideActive => StateMachine != null && !(StateMachine.CurrentState is State_Autonomous);

        #endregion

        #region 基础

        public void Initialize(MaidProfileData profileData, CharacterMainControl player, AICharacterController preCachedAI = null)
        {
            MainOwner = player;
            
            AI = preCachedAI != null ? preCachedAI : GetComponentInChildren<AICharacterController>();
            if (AI == null)
            {
                CMDebug.LogError($"严重错误：找不到 AICharacterController！");
                return;
            }

            // 注册实例
            if (!_maidRegistry.ContainsKey(AI)) _maidRegistry.Add(AI, this);
            
            _cachedCharacter = MaidCharacter;
            if (_cachedCharacter != null)
            {
                _cachedCharacter.BeforeCharacterSpawnLootOnDead += OnCheckLootBeforeDeath;
            }
            
            // 初始化技能系统
            SkillSystem = gameObject.GetComponent<MaidSkillComponent>() ?? gameObject.AddComponent<MaidSkillComponent>();
            SkillSystem.Initialize(this);
            
            // 固有技能：小队协同
            SkillSystem.AddSkill(new Skill_SquadCoordination());
            
            // 加载配置技能
            if (profileData.ExtraData?.Skills != null)
            {
                foreach (var skillConfig in profileData.ExtraData.Skills)
                {
                    var skill = MaidSkillFactory.CreateSkill(skillConfig);
                    if (skill != null) SkillSystem.AddSkill(skill);
                }
            }
    
            // 初始化状态机
            InitializeStateMachine();
    
            CMDebug.Log($"女仆控制器初始化完成。主人: {player.name}, 技能数: {SkillSystem.SkillCount}");
        }

        private void Update()
        {
            if (AI == null || MaidCharacter == null || MaidCharacter.Health.IsDead) return;
            StateMachine?.Update();
        }

        private void OnDestroy()
        {
            if (_cachedCharacter != null)
            {
                _cachedCharacter.BeforeCharacterSpawnLootOnDead -= OnCheckLootBeforeDeath;
            }
            if (AI != null && _maidRegistry.ContainsKey(AI))
            {
                _maidRegistry.Remove(AI);
            }
        }

        #endregion

        #region 状态机初始化

        private void InitializeStateMachine()
        {
            StateMachine = new MaidStateMachine(this);

            StateMachine.AddState(new State_Autonomous());
            StateMachine.AddState(new State_TacticalMove());
            StateMachine.AddState(new State_ForceFollow());
            StateMachine.AddState(new State_HoldPosition());
            StateMachine.AddState(new State_PassiveFollow());
            StateMachine.AddState(new State_Scavenge());
            StateMachine.AddState(new State_InventoryManage());

            StateMachine.ChangeState<State_Autonomous>();
        }

        #endregion

        #region 对外api

        /// <summary>
        /// 强制移动指令 (G键)
        /// </summary>
        public void ForceMoveTo(Vector3 position)
        {
            // 检查当前是否已经是战术移动状态
            if (StateMachine.CurrentState is State_TacticalMove tacticalState)
            {
                // 1. 更新目标点
                tacticalState.TargetPosition = position;
                // 2. 强制“重入”状态
                // 重新触发 Enter() 里的 AI.MoveToPos 逻辑
                // 从而打断当前的卡死状态，执行新的移动
                tacticalState.Enter(); 
                CMDebug.Log($"[MaidController] 刷新战术移动目标 -> {position}");
                return;
            }

            // 如果是其他状态，走正常流程切换
            StateMachine.ChangeState<State_TacticalMove>(state => 
            {
                state.TargetPosition = position;
            });
        }
        
        /// <summary>
        /// 强制治疗指令 (H键)
        /// </summary>
        public void ForceHeal()
        {
            if (SkillSystem == null) return;

            var healSkill = SkillSystem.GetSkill<Skill_SelfHeal>();
            if (healSkill != null)
            {
                // CMDebug.Log($"{MaidCharacter.name} 收到强制治疗指令...");
                healSkill.ForceActivate(); 
            }
        }
        
        /// <summary>
        /// 切换驻守状态 (J键)
        /// </summary>
        public void ToggleHoldPosition()
        {
            // 如果当前已经是驻守状态 -> 解除驻守，切回自主模式
            if (StateMachine.CurrentState is State_HoldPosition)
            {
                StateMachine.ChangeState<State_Autonomous>();
            }
            // 如果当前不是驻守状态 -> 进入驻守模式
            else
            {
                StateMachine.ChangeState<State_HoldPosition>();
            }
        }
        
        /// <summary>
        /// 指挥搜刮 (L键)
        /// </summary>
        public void CommandScavenge()
        {
            if (LevelManager.Instance != null && LevelManager.Instance.IsBaseLevel)
            {
                MaidCharacter?.PopText("这是家里，不可以乱拿东西！");
                return;
            }
            
            if (StateMachine.CurrentState is State_Scavenge)
            {
                StateMachine.ChangeState<State_Autonomous>(); // 再次按键取消
                MaidCharacter?.PopText("取消搜刮");
            }
            else
            {
                StateMachine.ChangeState<State_Scavenge>();
            }
        }
        
        /// <summary>
        /// 吐出物品 (K键)
        /// </summary>
        public void CommandDumpLoot()
        {
            // 1. 基础检查
            if (MaidCharacter?.CharacterItem?.Inventory == null) return;

            // 2. 筛选出有效且在背包中的物品
            var validItems = LootHistory
                .Where(item => item != null && IsItemInInventory(item))
                .ToList();

            // 3. 如果没有东西，直接清理历史并退出
            if (validItems.Count == 0)
            {
                MaidCharacter.PopText("主人我身上没有东西了。。。");
                LootHistory.Clear();
                return;
            }

            // 4. 生成容器
            var container = SpawnDropContainer(validItems.Count);
            if (container == null || container.Inventory == null)
            {
                CMDebug.LogError("战利品箱生成失败！");
                return;
            }

            // 5. 批量转移物品
            foreach (var item in validItems)
            {
                container.Inventory.AddAndMerge(item, 0);
            }
            LootHistory.Clear();
        }
        
        /// <summary>
        /// 切换库存管理模式 (B键)
        /// </summary>
        public void ToggleInventoryManagement()
        {
            if (!SkillTreeManager.Instance.IsSkillUnlocked("maid_backpack_access"))
            {
                MaidCharacter?.PopText("不许看人家的私人物品！");
                return;
            }
            
            if (StateMachine.CurrentState is State_InventoryManage)
            {
                StateMachine.ChangeState<State_Autonomous>();
            }
            else
            {
                float dist = Vector3.Distance(transform.position, MainOwner.transform.position);
                if (dist > 5.0f) 
                {
                    MaidCharacter?.PopText("主人，请靠近一点...");
                    return; 
                }
                
                StateMachine.ChangeState<State_InventoryManage>();
            }
        }
        
        public void TogglePassiveFollow()
        {
            if (StateMachine.CurrentState is State_PassiveFollow)
            {
                StateMachine.ChangeState<State_Autonomous>();
                MaidCharacter?.PopText("进入自由模式");
            }
            else
            {
                StateMachine.ChangeState<State_PassiveFollow>();
                MaidCharacter?.PopText("进入和平模式");
            }
        }

        
        #endregion
        
        #region 感官屏蔽系统

        private bool _isSensorySuppressed = false;
        
        // 缓存原始感官数据
        private float _cachedSightDistance;
        private float _cachedHearingAbility;
        private float _cachedSightAngle;
        private float _cachedForceTraceDist;

        /// <summary>
        /// 设置是否屏蔽索敌感知能力
        /// </summary>
        /// <param name="shouldSuppress">true=致盲(屏蔽索敌), false=恢复正常</param>
        public void SetSensorySuppression(bool shouldSuppress)
        {
            if (AI == null) return;

            if (shouldSuppress)
            {
                if (_isSensorySuppressed) return;

                _cachedSightDistance = AI.sightDistance;
                _cachedHearingAbility = AI.hearingAbility;
                _cachedSightAngle = AI.sightAngle;
                _cachedForceTraceDist = AI.forceTracePlayerDistance;

                AI.sightDistance = 0f;
                AI.sightAngle = 0f;
                AI.hearingAbility = 0f;
                AI.forceTracePlayerDistance = 0f;
                
                ClearImmediateThreats();

                _isSensorySuppressed = true;
            }
            else
            {
                if (!_isSensorySuppressed) return;

                // 还原数据
                AI.sightDistance = _cachedSightDistance;
                AI.sightAngle = _cachedSightAngle;
                AI.hearingAbility = _cachedHearingAbility;
                AI.forceTracePlayerDistance = _cachedForceTraceDist;

                _isSensorySuppressed = false;
            }
        }

        /// <summary>
        /// 强制清除当前的仇恨目标和警觉状态
        /// </summary>
        public void ClearImmediateThreats()
        {
            if (AI == null) return;
            
            bool hadTarget = AI.searchedEnemy != null || AI.noticed;
            
            AI.searchedEnemy = null;
            AI.aimTarget = null;
            AI.noticed = false;
            AI.alert = false;
        }

        #endregion
        
        #region 内部辅助方法
        
        /// <summary>
        /// 生成一个临时战利品箱
        /// </summary>
        private InteractableLootbox SpawnDropContainer(int requiredCapacity)
        {
            var prefab = MaidCharacter.deadLootBoxPrefab;
            if (prefab == null) return null;

            Vector3 spawnPos = MaidCharacter.transform.position + Vector3.up * 1.5f + MaidCharacter.transform.forward * 0.5f;

            var boxInstance = Instantiate(prefab, spawnPos, MaidCharacter.transform.rotation);

            MultiSceneCore.MoveToActiveWithScene(boxInstance.gameObject, SceneManager.GetActiveScene().buildIndex);

            var rb = boxInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true; 
                Vector3 throwForce = MaidCharacter.transform.forward * 3.0f + Vector3.up * 2.0f;
                rb.velocity = throwForce;
                rb.angularVelocity = Random.insideUnitSphere * 5f;
            }
            if (boxInstance.interactCollider != null)
            {
                boxInstance.interactCollider.isTrigger = false;
            }
            else
            {
                var col = boxInstance.GetComponent<Collider>();
                if (col != null) col.isTrigger = false;
            }
            if (boxInstance.Inventory != null)
            {
                int safeCapacity = Mathf.Max(20, requiredCapacity + 10);
                boxInstance.Inventory.SetCapacity(safeCapacity);
            }

            return boxInstance;
        }

        /// <summary>
        /// 辅助检查：物品是否在背包内
        /// </summary>
        private bool IsItemInInventory(Item item)
        {
            return MaidCharacter.CharacterItem.Inventory.Contains(item);
        }
        
        /// <summary>
        /// 检查是否距离主人过远
        /// </summary>
        public bool IsTooFarFromOwner()
        {
            if (MainOwner == null) return false;
            
            float dist = Vector3.Distance(transform.position, MainOwner.transform.position);
            
            // 紧急情况传送判断交给状态机，此处仅返回距离阈值
            if (dist > TeleportDistance) return true;
            return dist > ForceFollowDistance;
        }
        
        /// <summary>
        /// 在角色死亡前一刻触发物资抢救
        /// </summary>
        private void OnCheckLootBeforeDeath(DamageInfo info)
        {
            CMDebug.LogInfo($"[{_cachedCharacter.characterPreset.DisplayName}] 临死前触发物资抢救...");
            CommandDumpLoot();
        }

        #endregion
    }
}