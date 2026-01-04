using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CombatMaid.Core.AttributeModifiers;
using CombatMaid.Core.CustomModel;
using UnityEngine;
using Duckov.Utilities;
using SodaCraft.Localizations;
using CombatMaid.Core.MaidConfigs;
using CombatMaid.Core.WineFox;
using CombatMaid.Settings;
using Cysharp.Threading.Tasks;
using Duckov.Scenes;
using Newtonsoft.Json;

namespace CombatMaid.Core
{
    public class MaidSpawner : MonoBehaviour
    {
        public static MaidSpawner Instance { get; private set; }

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

        #region 数据

        private Egg _eggPrefab;
        private bool _isInitialized = false;

        // 临时预设缓存
        private List<CharacterRandomPreset> _tempPresets = new List<CharacterRandomPreset>();

        // 游戏原生预设库
        private Dictionary<string, CharacterRandomPreset> _gameNativePresetMap =
            new Dictionary<string, CharacterRandomPreset>();

        // 生成预设缓存
        private Dictionary<string, CharacterRandomPreset> _generatedPresetsCache =
            new Dictionary<string, CharacterRandomPreset>();

        // 自定义 JSON 配置库
        private Dictionary<string, MaidProfileData> _maidProfiles = new Dictionary<string, MaidProfileData>();

        // 缓存的倍率快照
        private Vector3 _cachedMultipliers = Vector3.one;

        #endregion

        #region 初始化

        private IEnumerator InitializeRoutine()
        {
            // --- 关卡依赖部分 ---
            while (CharacterMainControl.Main == null) yield return null;

            if (_eggPrefab == null)
            {
                Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
                if (eggs.Length > 0) _eggPrefab = eggs[0];
            }

            // 获取游戏原生预设数据（用于生成基底）
            while (GameplayDataSettings.CharacterRandomPresetData == null) yield return null;

            CMDebug.LogInfo("正在预热 Buff 系统缓存...");
            CombatMaid.Core.BuffsSystem.MaidBuffUtils.Initialize();

            var allPresets = GameplayDataSettings.CharacterRandomPresetData.presets;
            _gameNativePresetMap.Clear();
            foreach (var preset in allPresets)
            {
                if (preset != null && !string.IsNullOrEmpty(preset.nameKey) &&
                    !_gameNativePresetMap.ContainsKey(preset.nameKey))
                {
                    _gameNativePresetMap.Add(preset.nameKey, preset);
                }
            }


            _isInitialized = true;
            CMDebug.LogInfo("MaidSpawner 逻辑运行环境初始化完成。");
        }

        public void LoadAllCustomPresets()
        {
            _maidProfiles.Clear();

            // 路径准备
            string modAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string sourceDir = Path.Combine(modAssemblyDir, "MaidPreset");
            string gameRootDir = Directory.GetCurrentDirectory();
            string saveRootDir = Path.Combine(gameRootDir, "CombatMaidSaves");

            // 确保存档根目录存在
            if (!Directory.Exists(saveRootDir)) Directory.CreateDirectory(saveRootDir);

            try
            {
                string[] sourceFiles = Directory.GetFiles(sourceDir, "*.json");
                foreach (string sourcePath in sourceFiles)
                {
                    string fileName = Path.GetFileName(sourcePath);
                    MaidProfileData dataToCache = null;

                    // --- 1. 处理酒狐 (RoyalMaid_WineFox) ---
                    if (fileName.Contains("WineFox"))
                    {
                        // 重点：直接使用 WineFoxDataManager 加载！
                        // 因为它内部已经处理了“优先读取根目录存档，没有则初始化”的逻辑
                        dataToCache = WineFoxDataManager.LoadOrInit();
                    }
                    // --- 2. 处理瓶中女仆 (Vial 系列) ---
                    else if (fileName.StartsWith("Vial", StringComparison.OrdinalIgnoreCase))
                    {
                        string vialSaveDir = Path.Combine(saveRootDir, "VialMaid");
                        if (!Directory.Exists(vialSaveDir)) Directory.CreateDirectory(vialSaveDir);

                        string userPath = Path.Combine(vialSaveDir, fileName);
                        // 首次运行，复制模组预设到存档区
                        if (!File.Exists(userPath)) File.Copy(sourcePath, userPath);

                        // 从存档区读取
                        dataToCache = LoadProfileFromPath(userPath);
                    }
                    // --- 3. 处理其他通用预设 ---
                    else
                    {
                        dataToCache = LoadProfileFromPath(sourcePath);
                    }

                    // 写入内存缓存，供 EarlyInitialize 中的 DCM 注册使用
                    if (dataToCache != null && !string.IsNullOrEmpty(dataToCache.ProfileName))
                    {
                        _maidProfiles[dataToCache.ProfileName] = dataToCache;
                        CMDebug.Log($"[初始化] 已同步配置缓存: {dataToCache.ProfileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"[初始化] 加载预设失败: {ex.Message}");
            }
        }

        private MaidProfileData LoadProfileFromPath(string path)
        {
            try
            {
                string jsonContent = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<MaidProfileData>(jsonContent);
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"解析 JSON 失败 {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        public void EarlyInitialize()
        {
            // 1. 加载所有 JSON 配置
            LoadAllCustomPresets();

            // 2. 立即执行同步注册（白名单 + 本地化 + 模型 ID）
            RegisterMaidsToDcm();

            CMDebug.Log("[MaidSpawner] 早期初始化完成：已同步女仆身份与模型预设。");
        }

        private void RegisterMaidsToDcm()
        {
            if (!CustomModelBridge.IsAvailable()) return;

            foreach (var profile in _maidProfiles.Values)
            {
                // 确保名字使用的是最新的（支持玩家存档中的自定义名字）
                string displayName = !string.IsNullOrEmpty(profile.PresetConfig.CustomName) 
                    ? profile.PresetConfig.CustomName : "战斗女仆";
        
                string modelId = profile.ExtraData?.CustomModelID ?? "10004";

                // 完全按照 API 方式注册为 Extension
                CustomModelBridge.RegisterMaid(profile.ProfileName, displayName, modelId);
            }
        }

// 辅助函数：统一 Key 生成规则
        private string GetMaidNameKey(MaidProfileData profile)
        {
            string baseKey = profile.ExtraData?.BasePresetKey ?? "Cname_Usec";
            return baseKey + $"_CM_{profile.ProfileName}";
        }

        #endregion

        #region 生成女仆api

        /// <summary>
        /// 生成酒狐 (自动处理存档读取)
        /// </summary>
        public void SpawnWineFox(Vector3 position, Action<MaidController> onComplete = null)
        {
            SpawnMaidByProfile("RoyalMaid_WineFox", position, onComplete);
        }

        /// <summary>
        /// 根据 ProfileName 生成女仆
        /// </summary>
        public void SpawnMaidByProfile(string profileName, Vector3 targetPos, Action<MaidController> onComplete = null)
        {
            if (!_isInitialized || LevelManager.Instance?.MainCharacter == null) return;

            MaidProfileData finalData = null;
            bool isWineFox = (profileName == "RoyalMaid_WineFox");

            if (isWineFox)
            {
                // 酒狐：强制从存档加载最新数据
                finalData = WineFoxDataManager.LoadOrInit();
            }
            else
            {
                // 普通女仆：从内存缓存加载
                _maidProfiles.TryGetValue(profileName, out finalData);
            }

            if (finalData == null)
            {
                CMDebug.LogError($"无法生成: 未找到配置 [{profileName}]");
                return;
            }

            SpawnInternal(targetPos, finalData, (aiCtrl) =>
            {
                var controller = AssemblyMaidComponents(aiCtrl, finalData);

                // 酒狐要挂载数据同步器
                if (isWineFox && controller != null)
                {
                    var sync = controller.gameObject.AddComponent<WineFoxDataSync>();
                    sync.Initialize(controller, finalData);
                    sync.LoadInventory();
                    if (WineFoxDataManager.CurrentData.Inventory == null)
                    {
                        sync.SaveInventory();
                    }

                    CMDebug.Log($"[Spawn] 酒狐已生成 (存档同步开启)");
                }

                // 移交指挥权
                if (MaidManager.Instance != null && controller != null)
                {
                    MaidManager.Instance.RegisterActiveMaid(controller);
                }


                onComplete?.Invoke(controller);
            });
        }

        #endregion

        #region 内部生成api

        /// <summary>
        /// 生成流程：准备预设 -> 异步生成 -> 基础 AI 设置
        /// </summary>
        private void SpawnInternal(Vector3 targetPos, MaidProfileData profileData,
            Action<AICharacterController> callback)
        {
            Vector3 currentMultipliers = new Vector3(
                CombatMaidConfig.HealthMultiplier,
                CombatMaidConfig.AttackMultiplier,
                CombatMaidConfig.MoveSpeedMultiplier
            );

            if (_cachedMultipliers != currentMultipliers)
            {
                CMDebug.Log($"检测到倍率变化: {_cachedMultipliers} -> {currentMultipliers}，刷新预设缓存");
                _generatedPresetsCache.Clear();
                _cachedMultipliers = currentMultipliers;
            }

            var spawnConfig = profileData.PresetConfig;
            var extraData = profileData.ExtraData;
            string baseKey = extraData?.BasePresetKey ?? "Cname_Usec";

            if (!_gameNativePresetMap.TryGetValue(baseKey, out var sourcePreset))
            {
                CMDebug.LogError($"原生预设基底 '{baseKey}' 不存在，无法生成。");
                return;
            }

            CMDebug.Log($"正在生成 [{profileData.ProfileName}] (Base: {baseKey})...");

            CharacterRandomPreset finalPreset =
                CreateFullCustomPreset(sourcePreset, spawnConfig, profileData.ProfileName);
            _tempPresets.Add(finalPreset);

            SpawnAsync(finalPreset, targetPos, LevelManager.Instance.MainCharacter, callback).Forget();
        }

        private async UniTaskVoid SpawnAsync(CharacterRandomPreset preset, Vector3 position,
            CharacterMainControl player, Action<AICharacterController> callback)
        {
            try
            {
                if (_eggPrefab != null && _eggPrefab.spawnFx != null)
                {
                    Instantiate(_eggPrefab.spawnFx, position, Quaternion.identity);
                }

                int sceneIndex = MultiSceneCore.MainScene.HasValue
                    ? MultiSceneCore.MainScene.Value.buildIndex
                    : UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;

                CharacterMainControl spawnedChar = await preset.CreateCharacterAsync(
                    position + Vector3.down * 0.25f,
                    player.transform.forward,
                    sceneIndex,
                    null,
                    false
                );

                if (spawnedChar != null)
                {
                    AICharacterController ai = spawnedChar.GetComponentInChildren<AICharacterController>();
                    spawnedChar.SetPosition(position + Vector3.down * 0.25f);

                    if (ai != null)
                    {
                        var pet = ai.GetComponent<PetAI>();
                        if (pet != null) pet.SetMaster(player);

                        ai.leader = player;
                        spawnedChar.SetTeam(player.Team);

                        callback?.Invoke(ai);
                        CMDebug.Log($"生成成功: {spawnedChar.name}");
                    }
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"异步生成异常: {ex}");
            }
        }

        /// <summary>
        /// 组件组装车间：负责挂载 Controller、应用皮肤
        /// </summary>
        private MaidController AssemblyMaidComponents(AICharacterController ai, MaidProfileData profileData)
        {
            if (ai == null || ai.CharacterMainControl == null) return null;

            var charCtrl = ai.CharacterMainControl;

            // 1.挂载控制器
            var controller = charCtrl.gameObject.AddComponent<MaidController>();
            controller.Initialize(profileData, LevelManager.Instance.MainCharacter, ai);
            
            // 2.自定义模型处理
            if (CustomModelBridge.IsAvailable())
            {
                Type handlerType = CustomModelBridge.GetModelHandlerType();
                if (handlerType != null)
                {
                    // 在根节点挂载 ModelHandler
                    Component handler = charCtrl.GetComponent(handlerType);
                    if (handler == null) handler = charCtrl.gameObject.AddComponent(handlerType);

                    // 调用官方初始化和刷新流程，使模型生效
                    CustomModelBridge.ActivateModel(handler, charCtrl, profileData.ProfileName);
                }
            }

            // 3.背包扩容
            if (profileData.PresetConfig.InventoryCapacity != 0)
            {
                // (1) 修改数值统计 (Stat)，这确保了数据的正确性（例如UI显示上限）
                AttributeModifier.Modify(charCtrl, "InventoryCapacity", profileData.PresetConfig.InventoryCapacity,
                    false);

                // (2) 手动应用到 Inventory 对象
                // 原生 CharacterMainControl.UpdateInventoryCapacity 会跳过 NPC，所以必须手动设置
                if (charCtrl.CharacterItem != null && charCtrl.CharacterItem.Inventory != null)
                {
                    // 获取修改后的最终值 (Base + Modifiers)
                    int newCapacity = Mathf.RoundToInt(charCtrl.InventoryCapacity);

                    // 强制设置容量
                    charCtrl.CharacterItem.Inventory.SetCapacity(newCapacity);

                    CMDebug.Log(
                        $"[Spawn] 背包扩容成功: Stat增加 {profileData.PresetConfig.InventoryCapacity} -> 实际容量已同步为 {newCapacity}");
                }
                else
                {
                    CMDebug.LogWarning($"[Spawn] 背包扩容失败: {charCtrl.name} 的 Inventory 为空");
                }
            }

            if (profileData.PresetConfig.HeadArmor > 0)
            {
                AttributeModifier.Modify(charCtrl, AttributeModifier.StandardAttributes.HeadArmor,
                    profileData.PresetConfig.HeadArmor, false);
                CMDebug.Log($"[Spawn] 应用头部护甲: {profileData.PresetConfig.HeadArmor}");
            }

            if (profileData.PresetConfig.BodyArmor > 0)
            {
                AttributeModifier.Modify(charCtrl, AttributeModifier.StandardAttributes.BodyArmor,
                    profileData.PresetConfig.BodyArmor, false);
                CMDebug.Log($"[Spawn] 应用身体护甲: {profileData.PresetConfig.BodyArmor}");
            }

            return controller;
        }

        #endregion

        #region 预设管理

        /// <summary>
        /// 当技能树解锁导致属性变化时，立即刷新缓存中的酒狐数据
        /// </summary>
        public void RefreshWineFoxCache()
        {
            if (!_isInitialized) return;

            var currentData = WineFoxDataManager.CurrentData;
            if (currentData == null || currentData.PresetConfig == null) return;

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
                ApplyConfigToPreset(targetPreset, currentData.PresetConfig);
                CMDebug.Log("缓存中的酒狐数据已热更新！");
            }
        }

        private CharacterRandomPreset CreateFullCustomPreset(CharacterRandomPreset source, MaidConfig config,
            string profileName)
        {
            string uniqueSuffix = $"_CM_{profileName}";
            string finalKey = source.nameKey + uniqueSuffix;

            if (_generatedPresetsCache.TryGetValue(finalKey, out var cachedPreset))
            {
                return cachedPreset;
            }

            CharacterRandomPreset preset = Instantiate(source);
            preset.name = source.name + uniqueSuffix;
            preset.nameKey = finalKey;
            preset.team = Teams.player;

            // 在这里再次注册覆盖本地化名称，这里用的是玩家修改后的名称
            string displayName = !string.IsNullOrEmpty(config.CustomName) ? config.CustomName : "战斗女仆";
            LocalizationManager.SetOverrideText(finalKey, displayName);

            ApplyConfigToPreset(preset, config);
            // 给瓶中女仆默认强制加一个医疗箱
            if (profileName.IndexOf("Vial", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AppendItemToPreset(preset, 15);
            }

            _generatedPresetsCache.Add(finalKey, preset);

            return preset;
        }

        private void AppendItemToPreset(CharacterRandomPreset preset, int itemId)
        {
            var list = ReflectionHelper.GetPrivateField<IList>(preset, "itemsToGenerate");
            if (list == null) return;

            // 构建物品生成描述
            var desc = new RandomItemGenerateDescription
            {
                chance = 1f,
                randomCount = new Vector2Int(1, 1),
                randomFromPool = true,
                itemPool = new RandomContainer<RandomItemGenerateDescription.Entry>(),
                tags = new RandomContainer<Tag>(),
                addtionalRequireTags = new List<Tag>(),
                excludeTags = new List<Tag>(),
                qualities = new RandomContainer<int>(),
            };
            desc.itemPool.AddEntry(new RandomItemGenerateDescription.Entry { itemTypeID = itemId }, 100f);
            list.Add(desc);

            CMDebug.Log($"[MaidSpawner] 已强制为 {preset.name} 追加物品 ID: {itemId}");
        }

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

            // === 3. 反应与射击 ===
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

            preset.health *= CombatMaidConfig.HealthMultiplier;
            preset.damageMultiplier *= CombatMaidConfig.AttackMultiplier;
            preset.moveSpeedFactor *= CombatMaidConfig.MoveSpeedMultiplier;
        }

        private void SetupInventory(CharacterRandomPreset preset, List<int> itemIDs)
        {
            var list = ReflectionHelper.GetPrivateField<IList>(preset, "itemsToGenerate");
            if (list != null)
            {
                list.Clear();
                foreach (int id in itemIDs)
                {
                    // 修复：显式初始化所有引用类型字段，防止其他模组或游戏逻辑访问空对象报错
                    var desc = new RandomItemGenerateDescription
                    {
                        chance = 1f,
                        randomCount = new Vector2Int(1, 1),
                        randomFromPool = true,
                        itemPool = new RandomContainer<RandomItemGenerateDescription.Entry>(),

                        // === 必须初始化的字段 ===
                        tags = new RandomContainer<Tag>(), // 初始化标签容器
                        addtionalRequireTags = new List<Tag>(), // 初始化额外需求标签列表
                        excludeTags = new List<Tag>(), // 初始化排除标签列表
                        qualities = new RandomContainer<int>(), // 初始化品质容器
                    };

                    // 添加物品到池中
                    desc.itemPool.AddEntry(new RandomItemGenerateDescription.Entry { itemTypeID = id }, 100f);

                    list.Add(desc);
                }
            }
        }

        private void LoadSinglePreset(string path)
        {
            try
            {
                string jsonContent = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<MaidProfileData>(jsonContent);

                if (data != null && !string.IsNullOrEmpty(data.ProfileName))
                {
                    if (!_maidProfiles.ContainsKey(data.ProfileName))
                    {
                        _maidProfiles.Add(data.ProfileName, data);
                        CMDebug.Log($"已加载预设: {data.ProfileName} (来源: {Path.GetFileName(Path.GetDirectoryName(path))})");
                    }
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"解析失败 {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        #endregion

        #region Debug函数

        public void DebugListAllKeys()
        {
            if (!_isInitialized) return;
            CMDebug.Log("========== [可用预设列表] ==========");
            foreach (var key in _gameNativePresetMap.Keys) CMDebug.Log($"- {key}");
            CMDebug.Log("==================================");
        }

        public void DebugExportReferenceStats()
        {
            if (!_isInitialized) return;
            CMDebug.Log("========== 开始导出参考数值 (Keys) ==========");
            foreach (var key in _gameNativePresetMap.Keys)
            {
                LogPresetDebugInfo(key);
            }

            CMDebug.Log("========== 导出结束 ==========");
        }

        public void LogPresetDebugInfo(string presetKey)
        {
            if (!_isInitialized)
            {
                CMDebug.LogWarning($"Spawner 未初始化，无法读取预设");
                return;
            }

            // 注意：这里使用了新变量名 _gameNativePresetMap
            if (!_gameNativePresetMap.TryGetValue(presetKey, out var p))
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
                    try
                    {
                        var pool = item.GetType().GetField("itemPool").GetValue(item);
                        var entries = pool.GetType().GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(pool) as IList;
                        if (entries != null)
                        {
                            foreach (var entry in entries)
                            {
                                var id = entry.GetType().GetField("itemTypeID").GetValue(entry);
                                sb.Append($"{id}, ");
                            }
                        }
                    }
                    catch
                    {
                    }
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

        #endregion
    }

    public static class ReflectionHelper
    {
        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var type = obj.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) field.SetValue(obj, value);
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