using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        private CharacterMainControl _cachedCharacter;
        
        public MaidAIAssistant AIAssistant { get; private set; }
        
        public Vector3? ManualMoveTarget { get; set; } = null;

        #endregion

        #region 默认设置

        public float HoldMaxDistance = 30.0f; // 驻守模式最大宽容距离
        public float TeleportDistance = 35.0f; // 强制传送距离
        public float TeleportTimeout = 5.0f; // 强制跟随卡死超时时间

        public float ForceFollowDistance = 25.0f; // 强制跟随触发距离
        public float SafeDistanceToResumeCombat = 8.0f; // 恢复自主战斗的安全距离

        /// <summary>
        /// 是否处于非原版接管状态
        /// </summary>
        public bool IsOverrideActive => StateMachine != null && !(StateMachine.CurrentState is State_Autonomous);

        #endregion

        #region 基础

        public void Initialize(MaidProfileData profileData, CharacterMainControl player,
            AICharacterController preCachedAI = null)
        {
            MainOwner = player;

            AI = preCachedAI != null ? preCachedAI : GetComponentInChildren<AICharacterController>();
            if (AI == null)
            {
                CMDebug.LogError($"严重错误：找不到 AICharacterController！");
                return;
            }

            if (!_maidRegistry.ContainsKey(AI)) _maidRegistry.Add(AI, this);

            _cachedCharacter = MaidCharacter;
            if (_cachedCharacter != null)
            {
                _cachedCharacter.BeforeCharacterSpawnLootOnDead += OnCheckLootBeforeDeath;
            }

            SkillSystem = gameObject.GetComponent<MaidSkillComponent>() ??
                          gameObject.AddComponent<MaidSkillComponent>();
            SkillSystem.Initialize(this);

            // 固有技能：小队协同
            SkillSystem.AddSkill(new Skill_SquadCoordination());
            // 固有技能：温暖
            SkillSystem.AddSkill(new Skill_WarmthOfFox());

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
            AIAssistant = new MaidAIAssistant(this);
            InitializeStateMachine();
            CMDebug.Log($"女仆控制器初始化完成。技能数: {SkillSystem.SkillCount}");
        }

        private void Update()
        {
            if (AI == null || MaidCharacter == null || MaidCharacter.Health.IsDead) return;
            //AIAssistant?.OnTick();
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
            if (StateMachine.CurrentState is State_TacticalMove tacticalState)
            {
                tacticalState.TargetPosition = position;
                // 重新触发 Enter() 里的 AI.MoveToPos 逻辑
                tacticalState.Enter();
                //CMDebug.Log($"刷新战术移动目标 -> {position}");
                return;
            }
            // 其他状态，走正常流程切换
            StateMachine.ChangeState<State_TacticalMove>(state => { state.TargetPosition = position; });
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
            else
            {
                MaidCharacter.PopText("我还不会这个技能...");
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
            if (MaidCharacter?.CharacterItem?.Inventory == null) return;
            if (LootHistory == null) return;

            var validItems = LootHistory
                .Where(item => item != null && IsItemInInventory(item))
                .ToList();

            if (validItems.Count == 0)
            {
                MaidCharacter.PopText("主人我身上没有东西了。。。");
                LootHistory.Clear();
                return;
            }

            var container = SpawnDropContainer(validItems.Count);
            if (container == null || container.Inventory == null)
            {
                CMDebug.LogError("战利品箱生成失败！");
                return;
            }

            foreach (var item in validItems)
            {
                if (item != null && IsItemInInventory(item))
                {
                    try 
                    {
                        container.Inventory.AddAndMerge(item, 0);
                    }
                    catch (System.Exception e)
                    {
                        CMDebug.LogWarning($"转移物品失败: {item.DisplayName} - {e.Message}");
                    }
                }
            }

            LootHistory.Clear();
            MaidCharacter.PopText("主人这是今天搜刮到的战利品！");
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
        /// 清除当前的仇恨目标和警觉状态
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
            if (prefab == null)
            {
                CMDebug.LogError("无法生成战利品箱：未配置 deadLootBoxPrefab！");
                return null;
            }

            // 角色正前方 0.6 米处
            Vector3 forwardOffset = MaidCharacter.transform.forward * 0.6f;
            Vector3 basePos = MaidCharacter.transform.position + forwardOffset;

            Vector3 spawnPos;
            if (Physics.Raycast(basePos + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 2.5f))
            {
                spawnPos = hit.point + Vector3.up * 0.05f;
            }
            else
            {
                spawnPos = basePos + Vector3.up * 0.2f;
                CMDebug.LogWarning($"前方未检测到地面，使用备用位置生成战利品箱: {spawnPos}");
            }

            var boxInstance = Instantiate(prefab, spawnPos, MaidCharacter.transform.rotation);
            MultiSceneCore.MoveToActiveWithScene(boxInstance.gameObject, SceneManager.GetActiveScene().buildIndex);

            var rb = boxInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true; // 保持重力
                rb.drag = 10f;
                rb.angularDrag = 10f;
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
            var inventory = MaidCharacter?.CharacterItem?.Inventory;
            return inventory != null && inventory.Contains(item);
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