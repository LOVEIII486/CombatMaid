using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Duckov.Utilities;
using Duckov.Modding;
using SodaCraft.Localizations;

namespace CombatMaid.Core
{
    /// <summary>
    /// 女仆生成管理器
    /// </summary>
    public class MaidSpawner : MonoBehaviour
    {
        private const string LogTag = "[CombatMaid.MaidSpawner]";

        public static MaidSpawner Instance { get; private set; }
        
        private const float SpawnCheckRadius = 5.0f;

        private Egg _eggPrefab;
        private bool _isInitialized = false;

        private List<CharacterRandomPreset> _tempPresets = new List<CharacterRandomPreset>();
        private Dictionary<string, CharacterRandomPreset> _presetMap = new Dictionary<string, CharacterRandomPreset>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
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
            Debug.Log($"{LogTag} 开始初始化...");

            // 1. 等待主角加载
            while (CharacterMainControl.Main == null)
            {
                yield return null;
            }

            // 2. 获取 Egg 预制体 (生成角色的容器)
            if (_eggPrefab == null)
            {
                Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
                if (eggs.Length > 0) _eggPrefab = eggs[0];
            }

            if (_eggPrefab == null)
            {
                Debug.LogError($"{LogTag} 严重错误：未能在 Resources 中找到 Egg 预制体！无法生成 AI。");
                yield break;
            }

            // 3. 等待并获取游戏原生预设数据
            while (GameplayDataSettings.CharacterRandomPresetData == null)
            {
                yield return null;
            }

            var allPresets = GameplayDataSettings.CharacterRandomPresetData.presets;
            _presetMap.Clear();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{LogTag} 已加载预设列表 (可用 Key):");

            foreach (var preset in allPresets)
            {
                if (preset == null) continue;

                if (!string.IsNullOrEmpty(preset.nameKey))
                {
                    if (!_presetMap.ContainsKey(preset.nameKey))
                    {
                        _presetMap.Add(preset.nameKey, preset);
                        sb.Append($"[{preset.nameKey}] "); // 记录日志方便查阅
                    }
                }
            }
            Debug.Log(sb.ToString()); // 打印所有可用 Key，方便你核对 Cname_Usec 是否正确

            _isInitialized = true; 
            Debug.Log($"{LogTag} 初始化完毕！共加载 {_presetMap.Count} 个预设。");
        }

        /// <summary>
        /// 异步生成女仆
        /// </summary>
        /// <param name="onSuccess">当AI成功实体化后的回调</param>
        public void SpawnMaid(string presetNameKey, Vector3 position, CharacterMainControl player,
            MaidConfig config, Action<AICharacterController> onSuccess)
        {
            if (!_isInitialized || _eggPrefab == null || player == null)
            {
                Debug.LogError($"{LogTag} 生成前置条件未满足。");
                return;
            }

            if (string.IsNullOrEmpty(presetNameKey) || !_presetMap.TryGetValue(presetNameKey, out var sourcePreset))
            {
                Debug.LogError($"{LogTag} 找不到名为 '{presetNameKey}' 的预设！");
                return;
            }

            try
            {
                if (config == null) config = new MaidConfig();

                CharacterRandomPreset finalPreset = CreateCustomPreset(sourcePreset, config);
                _tempPresets.Add(finalPreset);

                Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);
                // 忽略碰撞防止卡死
                var eggCol = egg.GetComponent<Collider>();
                var playerCol = player.GetComponent<Collider>();
                if(eggCol && playerCol) Physics.IgnoreCollision(eggCol, playerCol, true);

                // 设置稍长的孵化时间以确保安全
                float hatchTime = 0.05f; 
                egg.Init(position, player.transform.forward, player, finalPreset, hatchTime);

                Debug.Log($"{LogTag} 蛋已生成，等待孵化... (配置: {config.CustomName})");

                // 启动协程等待结果
                StartCoroutine(WaitForSpawnRoutine(position, hatchTime, onSuccess));
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 生成过程异常: {ex}");
            }
        }

        /// <summary>
        /// 等待蛋破壳并寻找 AI 的协程
        /// </summary>
        private IEnumerator WaitForSpawnRoutine(Vector3 pos, float hatchTime, Action<AICharacterController> callback)
        {
            // 先等待孵化时间 + 一点缓冲
            yield return new WaitForSeconds(hatchTime + 0.1f);

            float timeout = 2.0f; // 最多找2秒
            AICharacterController targetAI = null;

            while (timeout > 0)
            {
                targetAI = FindJustSpawnedAI(pos);
                if (targetAI != null)
                {
                    // 找到了！
                    callback?.Invoke(targetAI);
                    yield break;
                }
                
                // 没找到，等下一帧继续找
                timeout -= Time.deltaTime;
                yield return null;
            }

            Debug.LogError($"{LogTag} 超时：蛋生成了，但未能捕获到生成的 AI 对象。");
        }

        private CharacterRandomPreset CreateCustomPreset(CharacterRandomPreset source, MaidConfig config)
        {
            CharacterRandomPreset preset = Instantiate(source);

            string uniqueSuffix = $"_CombatMaid";
            string finalKey = source.nameKey + uniqueSuffix;
            
            Debug.Log($"randompreset source{source.name}");
            preset.name = source.name + uniqueSuffix;
            preset.nameKey = finalKey;
            
            string displayName = !string.IsNullOrEmpty(config.CustomName) ? config.CustomName : "战斗女仆_00";
            if (LocalizationManager.overrideTexts != null)
            {
                LocalizationManager.overrideTexts[finalKey] = displayName;
            }

            // === 基础数值 ===
            preset.health = config.Health;
            preset.moveSpeedFactor = config.MoveSpeedFactor;
            preset.team = Teams.player;

            // === 战斗参数 ===
            preset.damageMultiplier = config.DamageMultiplier;
            preset.reactionTime = config.ReactionTime;
            preset.sightDistance *= config.SightDistanceMultiplier;
            preset.hearingAbility = config.HearingAbility;
            preset.shootCanMove = config.ShootCanMove;
            preset.canDash = config.CanDash;

            // === UI 设置 ===
            preset.showHealthBar = config.ShowHealthBar;
            preset.showName = config.ShowName;

            // === 高级属性 ===
            ApplyReflectionConfig(preset, config);

            return preset;
        }

        private void ApplyReflectionConfig(CharacterRandomPreset preset, MaidConfig config)
        {
            // 修改Boss 图标
            if (config.IsBossIcon)
            {
                ReflectionHelper.SetFieldValue(preset, "characterIconType", CharacterIconTypes.boss);
            }

            // 自定义装备
            if (config.CustomItemIDs != null && config.CustomItemIDs.Count > 0)
            {
                SetupInventory(preset, config.CustomItemIDs);
            }
        }

        private void SetupInventory(CharacterRandomPreset preset, List<int> itemIDs)
        {
            FieldInfo field = typeof(CharacterRandomPreset).GetField("itemsToGenerate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var list = field.GetValue(preset) as IList;
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


        public Vector3 GetMousePosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Default", "Ground", "Terrain")))
                return hit.point;
            return Vector3.zero;
        }
    }

    /// <summary>
    /// 包含所有属性的配置类
    /// </summary>
    [System.Serializable]
    public class MaidConfig
    {
        [Header("核心")] public string CustomName = "战斗女仆";

        [Header("基础")] public float Health = 250f;
        public float MoveSpeedFactor = 1.3f;

        [Header("战斗")] public float DamageMultiplier = 1.0f;
        public float ReactionTime = 0.2f;
        public bool ShootCanMove = true;
        public bool CanDash = true;

        [Header("感知")] public float SightDistanceMultiplier = 1.5f;
        public float HearingAbility = 1.0f;

        [Header("外观与物品")] public bool ShowName = true;
        public bool ShowHealthBar = true;
        public bool IsBossIcon = false;
        public List<int> CustomItemIDs = new List<int>();
    }

    public static class ReflectionHelper
    {
        public static void SetFieldValue(object obj, string fieldName, object value)
        {
            FieldInfo field = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) field.SetValue(obj, value);
        }
    }
}