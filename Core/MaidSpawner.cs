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
using CombatMaid.Core.WineFox;
using Cysharp.Threading.Tasks;
using Duckov.Scenes;

namespace CombatMaid.Core
{
    public class MaidSpawner : MonoBehaviour
    {
        public static MaidSpawner Instance { get; private set; }

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

            // 1. 准备预设
            if (config == null) config = new MaidConfig();
            CharacterRandomPreset finalPreset = CreateFullCustomPreset(sourcePreset, config, profileName);
            _tempPresets.Add(finalPreset);

            // 2. 启动异步生成流程
            SpawnMaidDirectlyAsync(finalPreset, position, player, onSuccess).Forget();
        }

        private async UniTaskVoid SpawnMaidDirectlyAsync(CharacterRandomPreset preset, Vector3 position, 
            CharacterMainControl player, Action<AICharacterController> callback)
        {
            try 
            {
                // A. 播放特效 (借用 Egg 的特效资源)
                if (_eggPrefab != null && _eggPrefab.spawnFx != null)
                {
                    Instantiate(_eggPrefab.spawnFx, position, Quaternion.identity);
                }

                // B. 获取当前场景 Index
                int sceneIndex = 0;
                if (MultiSceneCore.MainScene.HasValue)
                {
                    sceneIndex = MultiSceneCore.MainScene.Value.buildIndex;
                }
                else
                {
                    // 如果拿不到 MainScene，就拿当前激活的场景
                    sceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                }

                // C. 直接异步生成角色，并获得返回值
                // 参数参考 Egg.cs: pos + down*0.25f, forward, sceneIndex, group=null, unk=false
                CharacterMainControl spawnedChar = await preset.CreateCharacterAsync(
                    position + Vector3.down * 0.25f, 
                    player.transform.forward,
                    sceneIndex, 
                    null, 
                    false
                );

                // D. 初始化 AI 关系
                if (spawnedChar != null)
                {
                    AICharacterController ai = spawnedChar.GetComponentInChildren<AICharacterController>();
                    
                    // 修正位置
                    spawnedChar.SetPosition(position + Vector3.down * 0.25f);

                    if (ai != null)
                    {
                        // 设置 PetAI
                        var petComponent = ai.GetComponent<PetAI>();
                        if (petComponent != null)
                        {
                            petComponent.SetMaster(player);
                        }

                        // 设置队长和队伍
                        ai.leader = player;
                        spawnedChar.SetTeam(player.Team);
                        
                        callback?.Invoke(ai);
                        CMDebug.Log($"精准生成成功: {spawnedChar.name}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                CMDebug.LogError($"异步生成失败: {ex}");
            }
        }
        
        /// <summary>
        /// 当技能树解锁导致属性变化时，调用此方法立即刷新缓存中的酒狐数据
        /// </summary>
        public void RefreshWineFoxCache()
        {
            if (!_isInitialized) return;

            // 1. 获取最新数据
            var currentData = WineFoxDataManager.CurrentData;
            if (currentData == null || currentData.PresetConfig == null) return;

            // 2. 构造缓存 Key (硬编码酒狐的 ProfileName)
            // 注意：这里需要确保和 CreateFullCustomPreset 里的命名逻辑一致
            // 假设原始 Key 是 "Cname_Usec" (BasePresetKey)，但我们通常不知道 Source 是哪个
            // 我们可以遍历缓存找到它，或者构建标准 Key
            
            // 更稳妥的方式：直接遍历缓存找到包含 "RoyalMaid_WineFox" 的项
            string targetKeyPart = "_CM_RoyalMaid_WineFox";
            
            CharacterRandomPreset targetPreset = null;
            foreach (var kvp in _generatedPresetsCache)
            {
                if (kvp.Key.EndsWith(targetKeyPart))
                {
                    targetPreset = kvp.Value;
                    break;
                }
            }

            if (targetPreset != null)
            {
                // 3. 原地刷新数据
                ApplyConfigToPreset(targetPreset, currentData.PresetConfig);
                CMDebug.Log("[MaidSpawner] 缓存中的酒狐数据已热更新！");
            }
            else
            {
                // 如果缓存里还没有（还没生成过），那就无所谓，下次生成会自动读取最新的
                CMDebug.Log("[MaidSpawner] 缓存中无酒狐实例，无需刷新。");
            }
        }

        // ==================== 预设配置逻辑 ====================

        /// <summary>
        /// 解析 MaidConfig 并应用
        /// </summary>
        private CharacterRandomPreset CreateFullCustomPreset(CharacterRandomPreset source, MaidConfig config,
            string profileName)
        {
            string uniqueSuffix = $"_CM_{profileName}";
            string finalKey = source.nameKey + uniqueSuffix;

            // [修改] 只要缓存有，就直接返回 (因为我们有了 Refresh 机制，缓存永远是最新的)
            if (_generatedPresetsCache.TryGetValue(finalKey, out var cachedPreset))
            {
                return cachedPreset; 
            }

            // 新建逻辑
            CharacterRandomPreset preset = Instantiate(source);
            preset.name = source.name + uniqueSuffix;
            preset.nameKey = finalKey;
            preset.team = Teams.player;

            // 注册本地化 (仅需一次)
            string displayName = !string.IsNullOrEmpty(config.CustomName) ? config.CustomName : "战斗女仆";
            if (LocalizationManager.overrideTexts != null)
            {
                LocalizationManager.overrideTexts[finalKey] = displayName;
            }

            // 应用属性
            ApplyConfigToPreset(preset, config);

            // 加入缓存
            _generatedPresetsCache.Add(finalKey, preset);

            return preset;
        }
        
        /// <summary>
        /// 将配置应用到预设对象 (核心数值逻辑)
        /// </summary>
        private void ApplyConfigToPreset(CharacterRandomPreset preset, MaidConfig config)
        {
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

            // === 7. 抗性 ===
            preset.elementFactor_Physics = config.ResistPhysics;
            preset.elementFactor_Fire = config.ResistFire;
            preset.elementFactor_Poison = config.ResistPoison;
            preset.elementFactor_Electricity = config.ResistElectricity;
            preset.elementFactor_Space = config.ResistSpace;
            preset.elementFactor_Ghost = config.ResistGhost;

            // === 8. 掉落与物品 ===
            preset.hasCashChance = config.HasCashChance;
            preset.cashRange = config.CashRange;
            preset.wantItem = config.WantItem;
            preset.dropBoxOnDead = config.DropBoxOnDead;

            if (config.CustomItemIDs != null && config.CustomItemIDs.Count > 0)
            {
                SetupInventory(preset, config.CustomItemIDs);
            }
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
        
        // ==================== 调试函数 ====================
        
        /// <summary>
        /// [调试] 输出所有已加载的预设 Key
        /// </summary>
        public void DebugListAllKeys()
        {
            if (!_isInitialized) return;
            
            CMDebug.Log("========== [可用预设列表] ==========");
            foreach (var key in _presetMap.Keys)
            {
                CMDebug.Log($"- {key}");
            }
            CMDebug.Log("==================================");
        }

        /// <summary>
        /// [调试] 批量输出官方参考数值
        /// </summary>
        public void DebugExportReferenceStats()
        {
            if (!_isInitialized)
            {
                CMDebug.LogWarning("MaidSpawner 尚未初始化，请稍后再试。");
                return;
            }

            // 这里列出你感兴趣的官方预设 ID
            string[] targetKeys = new string[]
            {
                "Cname_Usec",
                "Cname_Speedy",
                "Cname_Raider",
                "Cname_StormCreature",
                "Cname_Vida",
                "Cname_BALeader",
                "Cname_Boss_3Shot"
            };

            CMDebug.Log("========== 开始导出官方参考数值 ==========");
            
            foreach (var key in targetKeys)
            {
                if (_presetMap.ContainsKey(key))
                {
                    LogPresetDebugInfo(key); 
                }
                else
                {
                    CMDebug.LogWarning($"未找到官方预设: {key} (可能是拼写错误或该版本游戏未包含)");
                }
            }
            
            CMDebug.Log("========== 导出结束 ==========");
        }
        
        /// <summary>
        /// [调试] 输出原始预设的所有属性值
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