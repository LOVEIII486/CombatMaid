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
        public static MaidSpawner Instance { get; private set; }

        private const float SpawnCheckRadius = 5.0f;
        private Egg _eggPrefab;
        private bool _isInitialized = false;

        private List<CharacterRandomPreset> _tempPresets = new List<CharacterRandomPreset>();

        private Dictionary<string, CharacterRandomPreset> _presetMap = new Dictionary<string, CharacterRandomPreset>();
        
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
                CMDebug.LogError($"严重错误：未找到 Egg 预制体。");
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
                CMDebug.LogError($"预设 '{presetNameKey}' 不存在。");
                return;
            }

            try
            {
                if (config == null) config = new MaidConfig();
                
                CharacterRandomPreset finalPreset = CreateFullCustomPreset(sourcePreset, config, profileName);
                _tempPresets.Add(finalPreset);
                
                Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);
                float hatchTime = 0.05f;
                egg.Init(position, player.transform.forward, player, finalPreset, hatchTime);
                
                StartCoroutine(WaitForSpawnRoutine(position, hatchTime, onSuccess));
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"生成异常: {ex}");
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

            CMDebug.LogError($"生成超时。");
        }

        // ==================== 预设配置逻辑 ====================

        /// <summary>
        /// 全面解析 MaidConfig 并应用到 CharacterRandomPreset
        /// </summary>
        private CharacterRandomPreset CreateFullCustomPreset(CharacterRandomPreset source, MaidConfig config,
            string profileName)
        {
            // 1. 生成基于 ProfileName 的固定后缀
            string uniqueSuffix = $"_CM_{profileName}";
            string finalKey = source.nameKey + uniqueSuffix;

            // 2. 检查缓存
            // 如果这个预设之前已经生成过，直接返回缓存的实例，不再 Instantiate
            if (_generatedPresetsCache.TryGetValue(finalKey, out var cachedPreset))
            {
                return cachedPreset;
            }

            // 3. 缓存未命中，开始新建
            CharacterRandomPreset preset = Instantiate(source);
            LogPresetDebugInfo("Cname_Usec");
            
            preset.name = source.name + uniqueSuffix;
            preset.nameKey = finalKey;
            preset.team = Teams.player;

            // 注册本地化名称
            string displayName = !string.IsNullOrEmpty(config.CustomName) ? config.CustomName : "战斗女仆";
            if (LocalizationManager.overrideTexts != null)
            {
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

            // 4. 将新生成的预设加入缓存
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
        
        /// <summary>
        /// 输出原始预设的所有属性值
        /// </summary>
        public void LogPresetDebugInfo(string presetKey)
        {
            if (!_isInitialized)
            {
                CMDebug.LogWarning($"Spawner 未初始化，无法读取预设");
                return;
            }

            if (!_presetMap.TryGetValue(presetKey, out var p))
            {
                CMDebug.LogError($"找不到预设: {presetKey}");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== [原始预设参考数据: {presetKey}] ==========");
            
            // --- 1. 基础属性 ---
            sb.AppendLine("--- [基础属性] ---");
            sb.AppendLine($"Health: {p.health}");
            sb.AppendLine($"MoveSpeedFactor: {p.moveSpeedFactor}");
            sb.AppendLine($"HasSoul: {p.hasSoul}");
            sb.AppendLine($"Exp: {p.exp}");
            sb.AppendLine($"PushCharacter: {p.pushCharacter}");
            sb.AppendLine($"ShowName: {p.showName}");
            sb.AppendLine($"ShowHealthBar: {p.showHealthBar}");

            // --- 2. 感知能力 ---
            sb.AppendLine("\n--- [感知能力] ---");
            sb.AppendLine($"SightDistance: {p.sightDistance}");
            sb.AppendLine($"SightAngle: {p.sightAngle}");
            sb.AppendLine($"HearingAbility: {p.hearingAbility}");
            sb.AppendLine($"NightVisionAbility: {p.nightVisionAbility}");
            sb.AppendLine($"ForgetTime: {p.forgetTime}");
            sb.AppendLine($"SetActiveByPlayerDistance: {p.setActiveByPlayerDistance}");
            sb.AppendLine($"ForceTracePlayerDistance: {p.forceTracePlayerDistance}");
            sb.AppendLine($"TraceTargetChance: {p.minTraceTargetChance} ~ {p.maxTraceTargetChance}");

            // --- 3. 反应与射击 ---
            sb.AppendLine("\n--- [反应与射击] ---");
            sb.AppendLine($"ReactionTime: {p.reactionTime}");
            sb.AppendLine($"NightReactionTimeFactor: {p.nightReactionTimeFactor}");
            sb.AppendLine($"ShootDelay: {p.shootDelay}");
            sb.AppendLine($"ShootTimeRange: {p.shootTimeRange}");
            sb.AppendLine($"ShootTimeSpaceRange: {p.shootTimeSpaceRange}");
            sb.AppendLine($"ShootCanMove: {p.shootCanMove}");
            sb.AppendLine($"DefaultWeaponOut: {p.defaultWeaponOut}");

            // --- 4. 移动与战术 ---
            sb.AppendLine("\n--- [移动与战术] ---");
            sb.AppendLine($"PatrolRange: {p.patrolRange}");
            sb.AppendLine($"CombatMoveRange: {p.combatMoveRange}");
            sb.AppendLine($"CombatMoveTimeRange: {p.combatMoveTimeRange}");
            sb.AppendLine($"PatrolTurnSpeed: {p.patrolTurnSpeed}");
            sb.AppendLine($"CombatTurnSpeed: {p.combatTurnSpeed}");
            sb.AppendLine($"CanDash: {p.canDash}");
            sb.AppendLine($"DashCoolTimeRange: {p.dashCoolTimeRange}");
            sb.AppendLine($"CanTalk: {p.canTalk}");

            // --- 5. 战斗数值 ---
            sb.AppendLine("\n--- [战斗数值] ---");
            sb.AppendLine($"DamageMultiplier: {p.damageMultiplier}");
            sb.AppendLine($"BulletSpeedMultiplier: {p.bulletSpeedMultiplier}");
            sb.AppendLine($"GunDistanceMultiplier: {p.gunDistanceMultiplier}");
            sb.AppendLine($"GunScatterMultiplier: {p.gunScatterMultiplier}");
            sb.AppendLine($"ScatterMultiIfTargetRunning: {p.scatterMultiIfTargetRunning}");
            sb.AppendLine($"ScatterMultiIfOffScreen: {p.scatterMultiIfOffScreen}");
            sb.AppendLine($"GunCritRateGain: {p.gunCritRateGain}");
            sb.AppendLine($"AiCombatFactor: {p.aiCombatFactor}");

            // --- 6. 技能参数 ---
            sb.AppendLine("\n--- [技能参数] ---");
            sb.AppendLine($"HasSkill: {p.hasSkill}");
            sb.AppendLine($"HasSkillChance: {p.hasSkillChance}");
            sb.AppendLine($"SkillSuccessChance: {p.skillSuccessChance}");
            sb.AppendLine($"SkillCoolTimeRange: {p.skillCoolTimeRange}");

            // --- 7. 抗性 ---
            sb.AppendLine("\n--- [抗性] ---");
            sb.AppendLine($"ResistPhysics: {p.elementFactor_Physics}");
            sb.AppendLine($"ResistFire: {p.elementFactor_Fire}");
            sb.AppendLine($"ResistPoison: {p.elementFactor_Poison}");
            sb.AppendLine($"ResistElectricity: {p.elementFactor_Electricity}");
            sb.AppendLine($"ResistSpace: {p.elementFactor_Space}");
            sb.AppendLine($"ResistGhost: {p.elementFactor_Ghost}");

            // --- 8. 掉落与物品 ---
            sb.AppendLine("\n--- [掉落与物品] ---");
            sb.AppendLine($"HasCashChance: {p.hasCashChance}");
            sb.AppendLine($"CashRange: {p.cashRange}");
            sb.AppendLine($"WantItem: {p.wantItem}");
            sb.AppendLine($"DropBoxOnDead: {p.dropBoxOnDead}");

            // 读取物品列表
            var items = ReflectionHelper.GetPrivateField<IList>(p, "itemsToGenerate");
            if (items != null && items.Count > 0)
            {
                sb.Append("CustomItemIDs: [");
                foreach (var item in items)
                {
                    try {
                        var pool = item.GetType().GetField("itemPool").GetValue(item);
                        var entries = pool.GetType().GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool) as IList;
                        if (entries != null) {
                            foreach (var entry in entries) {
                                var id = entry.GetType().GetField("itemTypeID").GetValue(entry);
                                sb.Append($"{id}, ");
                            }
                        }
                    } catch {}
                }
                sb.AppendLine("]");
            }
            else
            {
                sb.AppendLine("CustomItemIDs: []");
            }

            sb.AppendLine("=============================================");

            CMDebug.Log(sb.ToString());
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
                CMDebug.LogWarning($"Field '{fieldName}' not found in type '{type.Name}'");
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