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
using Duckov.Scenes; // 可能包含 CustomFacePreset

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

        // 缓存反射类型，避免频繁查找
        private Type _facePresetType;
        private Type _featureInfoType;
        private Type _headSettingType;

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
            // 预加载反射类型 (尝试在几个常见位置查找)
            _facePresetType = Type.GetType("CustomFacePreset, TeamSoda.Duckov.Core") ?? Type.GetType("CustomFacePreset");
            
            // FeatureInfo 可能是嵌套类或独立类，尝试查找
            _featureInfoType = Type.GetType("CustomFacePreset+FeatureInfo, TeamSoda.Duckov.Core") 
                               ?? Type.GetType("FeatureInfo, TeamSoda.Duckov.Core")
                               ?? Type.GetType("CustomFacePreset+FeatureInfo")
                               ?? Type.GetType("FeatureInfo");

            _headSettingType = Type.GetType("CustomFacePreset+HeadSetting, TeamSoda.Duckov.Core")
                               ?? Type.GetType("HeadSetting, TeamSoda.Duckov.Core")
                               ?? Type.GetType("CustomFacePreset+HeadSetting")
                               ?? Type.GetType("HeadSetting");

            if (_facePresetType == null) Debug.LogError($"{LogTag} 严重警告：无法找到 CustomFacePreset 类型，捏脸功能将失效！");

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
            MaidConfig config, Action<AICharacterController> onSuccess)
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

                CharacterRandomPreset finalPreset = CreateFullCustomPreset(sourcePreset, config);
                _tempPresets.Add(finalPreset);

                Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);
                float hatchTime = 0.05f; 
                egg.Init(position, player.transform.forward, player, finalPreset, hatchTime);

                StartCoroutine(WaitForSpawnRoutine(position, hatchTime, config, onSuccess));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成异常: {ex}");
            }
        }

        private IEnumerator WaitForSpawnRoutine(Vector3 pos, float hatchTime, MaidConfig config, Action<AICharacterController> callback)
        {
            yield return new WaitForSeconds(hatchTime + 0.1f);
            float timeout = 2.0f;
            AICharacterController targetAI = null;

            while (timeout > 0)
            {
                targetAI = FindJustSpawnedAI(pos);
                if (targetAI != null)
                {
                    // 应用捏脸
                    if (config.FaceCode != null && config.FaceCode.savedSetting == false) // 简单的检查是否有捏脸数据
                    {
                        ApplyFaceData(targetAI, config);
                    }
                    
                    callback?.Invoke(targetAI);
                    yield break;
                }
                timeout -= Time.deltaTime;
                yield return null;
            }
            Debug.LogError($"{LogTag} 生成超时。");
        }

        // ==================== 捏脸应用逻辑 (修正版) ====================

        private void ApplyFaceData(AICharacterController ai, MaidConfig config)
        {
            if (_facePresetType == null || ai.CharacterMainControl == null || ai.CharacterMainControl.characterModel == null) return;

            try
            {
                // 1. 创建 CustomFacePreset 实例 (ScriptableObject)
                ScriptableObject facePreset = ScriptableObject.CreateInstance(_facePresetType);
                
                // 2. 将 Config 数据填充进 Preset
                PopulateFacePreset(facePreset, config.FaceCode);

                // 3. 调用 SetFaceFromPreset
                // 签名: public void SetFaceFromPreset(CustomFacePreset preset)
                var model = ai.CharacterMainControl.characterModel;
                var method = model.GetType().GetMethod("SetFaceFromPreset", new Type[] { _facePresetType });
                
                if (method != null)
                {
                    method.Invoke(model, new object[] { facePreset });
                    // Debug.Log($"{LogTag} 自定义捏脸应用成功！");
                }
                else
                {
                    Debug.LogError($"{LogTag} 找不到 SetFaceFromPreset 方法！");
                }
                
                // 临时对象，用完可以销毁 (如果是 ScriptableObject 实例)
                Destroy(facePreset);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 应用捏脸失败: {ex}");
            }
        }

        private void PopulateFacePreset(object preset, MaidFaceCode code)
        {
            // 头部
            if (code.headSetting != null && _headSettingType != null)
            {
                object headSet = Activator.CreateInstance(_headSettingType);
                ReflectionHelper.SetFieldValue(headSet, "mainColor", code.headSetting.mainColor.ToUnityColor());
                ReflectionHelper.SetFieldValue(headSet, "headScaleOffset", code.headSetting.headScaleOffset);
                ReflectionHelper.SetFieldValue(headSet, "foreheadHeight", code.headSetting.foreheadHeight);
                ReflectionHelper.SetFieldValue(headSet, "foreheadRound", code.headSetting.foreheadRound);
                ReflectionHelper.SetFieldValue(preset, "headSetting", headSet);
            }

            // 部位映射
            SetFeature(preset, "hair", code.hairID, code.hairInfo);
            SetFeature(preset, "eye", code.eyeID, code.eyeInfo);
            SetFeature(preset, "eyebrow", code.eyebrowID, code.eyebrowInfo);
            SetFeature(preset, "mouth", code.mouthID, code.mouthInfo);
            SetFeature(preset, "tail", code.tailID, code.tailInfo);
            SetFeature(preset, "foot", code.footID, code.footInfo);
            SetFeature(preset, "wing", code.wingID, code.wingInfo);
        }

        private void SetFeature(object preset, string prefix, int id, MaidFeatureInfo info)
        {
            if (info == null || _featureInfoType == null) return;

            // 设置 ID (例如 hairID)
            ReflectionHelper.SetFieldValue(preset, $"{prefix}ID", id);

            // 创建 FeatureInfo 对象
            object featureObj = Activator.CreateInstance(_featureInfoType);
            
            // 填充 FeatureInfo
            ReflectionHelper.SetFieldValue(featureObj, "radius", info.radius);
            ReflectionHelper.SetFieldValue(featureObj, "color", info.color.ToUnityColor());
            ReflectionHelper.SetFieldValue(featureObj, "height", info.height);
            ReflectionHelper.SetFieldValue(featureObj, "heightOffset", info.heightOffset);
            ReflectionHelper.SetFieldValue(featureObj, "scale", info.scale);
            ReflectionHelper.SetFieldValue(featureObj, "twist", info.twist);
            ReflectionHelper.SetFieldValue(featureObj, "distanceAngle", info.distanceAngle);
            ReflectionHelper.SetFieldValue(featureObj, "leftRightAngle", info.leftRightAngle);

            // 设置到 preset (例如 hairInfo)
            ReflectionHelper.SetFieldValue(preset, $"{prefix}Info", featureObj);
        }

        // ==================== 预设生成逻辑 (保持不变) ====================

        private CharacterRandomPreset CreateFullCustomPreset(CharacterRandomPreset source, MaidConfig config)
        {
            CharacterRandomPreset preset = Instantiate(source);
            string uniqueSuffix = $"_CM_{Guid.NewGuid().ToString().Substring(0, 4)}";
            string finalKey = source.nameKey + uniqueSuffix;
            
            preset.name = source.name + uniqueSuffix;
            preset.nameKey = finalKey;
            preset.team = Teams.player;

            string displayName = !string.IsNullOrEmpty(config.CustomName) ? config.CustomName : "战斗女仆";
            if (LocalizationManager.overrideTexts != null)
            {
                LocalizationManager.overrideTexts[finalKey] = displayName;
            }

            preset.health = config.Health;
            preset.moveSpeedFactor = config.MoveSpeedFactor;
            preset.hasSoul = config.HasSoul;
            preset.exp = config.Exp;
            preset.pushCharacter = config.PushCharacter;
            preset.showName = config.ShowName;
            preset.showHealthBar = config.ShowHealthBar;

            if (config.IsBossIcon) ReflectionHelper.SetPrivateField(preset, "characterIconType", CharacterIconTypes.boss);

            preset.sightDistance = config.SightDistance;
            preset.sightAngle = config.SightAngle;
            preset.hearingAbility = config.HearingAbility;
            preset.nightVisionAbility = config.NightVisionAbility;
            preset.forgetTime = config.ForgetTime;
            preset.setActiveByPlayerDistance = config.SetActiveByPlayerDistance;
            preset.forceTracePlayerDistance = config.ForceTracePlayerDistance;
            preset.minTraceTargetChance = config.MinTraceTargetChance;
            preset.maxTraceTargetChance = config.MaxTraceTargetChance;

            preset.reactionTime = config.ReactionTime;
            preset.nightReactionTimeFactor = config.NightReactionTimeFactor;
            preset.shootDelay = config.ShootDelay;
            preset.shootTimeRange = config.ShootTimeRange;
            preset.shootTimeSpaceRange = config.ShootTimeSpaceRange;
            preset.shootCanMove = config.ShootCanMove;
            preset.defaultWeaponOut = config.DefaultWeaponOut;

            preset.patrolRange = config.PatrolRange;
            preset.combatMoveRange = config.CombatMoveRange;
            preset.combatMoveTimeRange = config.CombatMoveTimeRange;
            preset.patrolTurnSpeed = config.PatrolTurnSpeed;
            preset.combatTurnSpeed = config.CombatTurnSpeed;
            preset.canDash = config.CanDash;
            preset.dashCoolTimeRange = config.DashCoolTimeRange;
            preset.canTalk = config.CanTalk;

            preset.damageMultiplier = config.DamageMultiplier;
            preset.bulletSpeedMultiplier = config.BulletSpeedMultiplier;
            preset.gunDistanceMultiplier = config.GunDistanceMultiplier;
            preset.gunScatterMultiplier = config.GunScatterMultiplier;
            preset.scatterMultiIfTargetRunning = config.ScatterMultiIfTargetRunning;
            preset.scatterMultiIfOffScreen = config.ScatterMultiIfOffScreen;
            preset.gunCritRateGain = config.GunCritRateGain;
            preset.aiCombatFactor = config.AiCombatFactor;

            preset.hasSkill = config.HasSkill;
            preset.hasSkillChance = config.HasSkillChance;
            preset.skillSuccessChance = config.SkillSuccessChance;
            preset.skillCoolTimeRange = config.SkillCoolTimeRange;
            
            preset.elementFactor_Physics = config.ResistPhysics;
            preset.elementFactor_Fire = config.ResistFire;
            preset.elementFactor_Poison = config.ResistPoison;
            preset.elementFactor_Electricity = config.ResistElectricity;
            preset.elementFactor_Space = config.ResistSpace;
            preset.elementFactor_Ghost = config.ResistGhost;

            preset.hasCashChance = config.HasCashChance;
            preset.cashRange = config.CashRange;
            preset.wantItem = config.WantItem;
            preset.dropBoxOnDead = config.DropBoxOnDead;

            if (config.CustomItemIDs != null && config.CustomItemIDs.Count > 0)
            {
                SetupInventory(preset, config.CustomItemIDs);
            }

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
        public static void SetFieldValue(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var type = obj.GetType();
            // 包含公有字段，以兼容 FeatureInfo 的公有字段
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

        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            SetFieldValue(obj, fieldName, value);
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