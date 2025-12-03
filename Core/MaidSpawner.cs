using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Duckov.Utilities;
using Duckov.Modding;
using SodaCraft.Localizations;
using ItemStatsSystem;
using CombatMaid.Core.MaidConfigs;

namespace CombatMaid.Core
{
    public class MaidSpawner : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidSpawner]";
        public static MaidSpawner Instance { get; private set; }

        private const float SpawnCheckRadius = 5.0f;
        private Egg _eggPrefab;
        private bool _isInitialized = false;

        private List<CharacterRandomPreset> _tempPresets = new List<CharacterRandomPreset>();

        private Dictionary<string, CharacterRandomPreset> _presetMap = new Dictionary<string, CharacterRandomPreset>();

        // [新增] 预设缓存池：防止同一个配置重复生成 ScriptableObject
        private Dictionary<string, CharacterRandomPreset> _generatedPresetsCache =
            new Dictionary<string, CharacterRandomPreset>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        private void Start()
        {
            StartCoroutine(InitializeRoutine());
        }

        private void OnDestroy()
        {
            foreach (var preset in _tempPresets)
            {
                if (preset != null) Destroy(preset);
            }

            _tempPresets.Clear();
        }

        private IEnumerator InitializeRoutine()
        {
            while (CharacterMainControl.Main == null) yield return null;

            if (_eggPrefab == null)
            {
                Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
                if (eggs.Length > 0) _eggPrefab = eggs[0];
            }

            if (_eggPrefab == null)
            {
                Debug.LogError($"{LogTag} 严重错误：未找到 Egg 预制体。");
                yield break;
            }

            while (GameplayDataSettings.CharacterRandomPresetData == null) yield return null;

            var allPresets = GameplayDataSettings.CharacterRandomPresetData.presets;
            _presetMap.Clear();
            foreach (var preset in allPresets)
            {
                if (preset != null && !string.IsNullOrEmpty(preset.nameKey) && !_presetMap.ContainsKey(preset.nameKey))
                {
                    _presetMap.Add(preset.nameKey, preset);
                }
            }

            _isInitialized = true;
        }

        public void SpawnMaid(string presetNameKey, Vector3 position, CharacterMainControl player,
            MaidConfig config, string profileName, Action<AICharacterController> onSuccess)
        {
            if (!_isInitialized || _eggPrefab == null || player == null) return;

            if (string.IsNullOrEmpty(presetNameKey) || !_presetMap.TryGetValue(presetNameKey, out var sourcePreset))
            {
                Debug.LogError($"{LogTag} 预设 '{presetNameKey}' 不存在。");
                return;
            }

            try
            {
                if (config == null) config = new MaidConfig();

                // 1. 创建并配置基础预设 (数值、物品等)
                CharacterRandomPreset finalPreset = CreateFullCustomPreset(sourcePreset, config, profileName);
                _tempPresets.Add(finalPreset);

                // 2. 生成蛋
                Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);
                float hatchTime = 0.05f;
                egg.Init(position, player.transform.forward, player, finalPreset, hatchTime);

                // 3. 等待生成并回调
                StartCoroutine(WaitForSpawnRoutine(position, hatchTime, onSuccess));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成异常: {ex}");
            }
        }

        private IEnumerator WaitForSpawnRoutine(Vector3 pos, float hatchTime, Action<AICharacterController> callback)
        {
            yield return new WaitForSeconds(hatchTime + 0.1f);
            float timeout = 2.0f;
            AICharacterController targetAI = null;

            while (timeout > 0)
            {
                targetAI = FindJustSpawnedAI(pos);
                if (targetAI != null)
                {
                    callback?.Invoke(targetAI);
                    yield break;
                }

                timeout -= Time.deltaTime;
                yield return null;
            }

            Debug.LogError($"{LogTag} 生成超时。");
        }

        // ==================== 预设配置逻辑 ====================

        /// <summary>
        /// 全面解析 MaidConfig 并应用到 CharacterRandomPreset (带缓存)
        /// </summary>
        private CharacterRandomPreset CreateFullCustomPreset(CharacterRandomPreset source, MaidConfig config,
            string profileName)
        {
            // 1. 生成基于 ProfileName 的固定后缀
            // 这样同一个配置生成的预设 ID 永远是相同的
            string uniqueSuffix = $"_CM_{profileName}";
            string finalKey = source.nameKey + uniqueSuffix;

            // 2. [核心优化] 检查缓存
            // 如果这个预设之前已经生成过，直接返回缓存的实例，不再 Instantiate
            if (_generatedPresetsCache.TryGetValue(finalKey, out var cachedPreset))
            {
                return cachedPreset;
            }

            // 3. 缓存未命中，开始新建
            CharacterRandomPreset preset = Instantiate(source);

            preset.name = source.name + uniqueSuffix; // Unity 资产名 (e.g. Cname_Usec_CM_RoyalMaid_Bella)
            preset.nameKey = finalKey; // 本地化 Key
            preset.team = Teams.player;

            // 注册本地化名称
            string displayName = !string.IsNullOrEmpty(config.CustomName) ? config.CustomName : "战斗女仆";
            if (LocalizationManager.overrideTexts != null)
            {
                // 使用索引器赋值，如果 Key 已存在会自动更新，不存在会自动添加
                LocalizationManager.overrideTexts[finalKey] = displayName;
            }

            // === 1. 基础属性 ===
            preset.health = config.Health;
            preset.moveSpeedFactor = config.MoveSpeedFactor;
            preset.hasSoul = config.HasSoul;
            preset.exp = config.Exp;
            preset.pushCharacter = config.PushCharacter;
            preset.showName = config.ShowName;
            preset.showHealthBar = config.ShowHealthBar;

            if (config.IsBossIcon)
                ReflectionHelper.SetPrivateField(preset, "characterIconType", CharacterIconTypes.boss);

            // === 2. 感知与 AI 逻辑 ===
            preset.sightDistance = config.SightDistance;
            preset.sightAngle = config.SightAngle;
            preset.hearingAbility = config.HearingAbility;
            preset.nightVisionAbility = config.NightVisionAbility;
            preset.forgetTime = config.ForgetTime;
            preset.setActiveByPlayerDistance = config.SetActiveByPlayerDistance;
            preset.forceTracePlayerDistance = config.ForceTracePlayerDistance;
            preset.minTraceTargetChance = config.MinTraceTargetChance;
            preset.maxTraceTargetChance = config.MaxTraceTargetChance;

            // === 3. 射击与反应 ===
            preset.reactionTime = config.ReactionTime;
            preset.nightReactionTimeFactor = config.NightReactionTimeFactor;
            preset.shootDelay = config.ShootDelay;
            preset.shootTimeRange = config.ShootTimeRange;
            preset.shootTimeSpaceRange = config.ShootTimeSpaceRange;
            preset.shootCanMove = config.ShootCanMove;
            preset.defaultWeaponOut = config.DefaultWeaponOut;

            // === 4. 移动与战术 ===
            preset.patrolRange = config.PatrolRange;
            preset.combatMoveRange = config.CombatMoveRange;
            preset.combatMoveTimeRange = config.CombatMoveTimeRange;
            preset.patrolTurnSpeed = config.PatrolTurnSpeed;
            preset.combatTurnSpeed = config.CombatTurnSpeed;
            preset.canDash = config.CanDash;
            preset.dashCoolTimeRange = config.DashCoolTimeRange;
            preset.canTalk = config.CanTalk;

            // === 5. 战斗数值 ===
            preset.damageMultiplier = config.DamageMultiplier;
            preset.bulletSpeedMultiplier = config.BulletSpeedMultiplier;
            preset.gunDistanceMultiplier = config.GunDistanceMultiplier;
            preset.gunScatterMultiplier = config.GunScatterMultiplier;
            preset.scatterMultiIfTargetRunning = config.ScatterMultiIfTargetRunning;
            preset.scatterMultiIfOffScreen = config.ScatterMultiIfOffScreen;
            preset.gunCritRateGain = config.GunCritRateGain;
            preset.aiCombatFactor = config.AiCombatFactor;

            // === 6. 技能配置 ===
            preset.hasSkill = config.HasSkill;
            preset.hasSkillChance = config.HasSkillChance;
            preset.skillSuccessChance = config.SkillSuccessChance;
            preset.skillCoolTimeRange = config.SkillCoolTimeRange;

            // === 7. 抗性 (Element Factors) ===
            preset.elementFactor_Physics = config.ResistPhysics;
            preset.elementFactor_Fire = config.ResistFire;
            preset.elementFactor_Poison = config.ResistPoison;
            preset.elementFactor_Electricity = config.ResistElectricity;
            preset.elementFactor_Space = config.ResistSpace;
            preset.elementFactor_Ghost = config.ResistGhost;

            // === 8. 掉落与物品 (Cash & Items) ===
            preset.hasCashChance = config.HasCashChance;
            preset.cashRange = config.CashRange;
            preset.wantItem = config.WantItem;
            preset.dropBoxOnDead = config.DropBoxOnDead;

            if (config.CustomItemIDs != null && config.CustomItemIDs.Count > 0)
            {
                SetupInventory(preset, config.CustomItemIDs);
            }

            // 4. [核心优化] 将新生成的预设加入缓存
            _generatedPresetsCache.Add(finalKey, preset);

            return preset;
        }

        private void SetupInventory(CharacterRandomPreset preset, List<int> itemIDs)
        {
            var list = ReflectionHelper.GetPrivateField<IList>(preset, "itemsToGenerate");
            if (list != null)
            {
                list.Clear();
                foreach (int id in itemIDs)
                {
                    var desc = new RandomItemGenerateDescription
                    {
                        chance = 1f,
                        randomCount = new Vector2Int(1, 1),
                        randomFromPool = true,
                        itemPool = new RandomContainer<RandomItemGenerateDescription.Entry>()
                    };
                    desc.itemPool.AddEntry(new RandomItemGenerateDescription.Entry { itemTypeID = id }, 100f);
                    list.Add(desc);
                }
            }
        }

        private AICharacterController FindJustSpawnedAI(Vector3 spawnPos)
        {
            var allAIs = FindObjectsOfType<AICharacterController>();
            AICharacterController bestFit = null;
            float minDistance = SpawnCheckRadius;
            foreach (var ai in allAIs)
            {
                if (ai.CharacterMainControl.Health.IsDead) continue;
                float dist = Vector3.Distance(ai.transform.position, spawnPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestFit = ai;
                }
            }

            return bestFit;
        }
    }

    public static class ReflectionHelper
    {
        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                Debug.LogWarning($"[Reflection] Field '{fieldName}' not found in type '{type.Name}'");
            }
        }

        public static T GetPrivateField<T>(object obj, string fieldName) where T : class
        {
            if (obj == null) return null;
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field?.GetValue(obj) as T;
        }
    }
}