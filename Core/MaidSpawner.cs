using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Duckov.Modding; // 根据实际情况调整命名空间

namespace CombatMaid
{
    /// <summary>
    /// 女仆生成管理器
    /// 结合了 SpawnHelper 的配置灵活性和原 Mod 的队友识别逻辑
    /// </summary>
    public class MaidSpawner : MonoBehaviour
    {
        public static MaidSpawner Instance { get; private set; }

        [Header("核心设置")]
        public float SpawnCheckRadius = 3.0f; // 生成后寻找AI的范围
        
        // 缓存资源
        private Egg _eggPrefab;
        private List<CharacterRandomPreset> _maidPresets = new List<CharacterRandomPreset>();
        private bool _isInitialized = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            StartCoroutine(InitializeResources());
        }

        /// <summary>
        /// 异步初始化资源，避免卡顿
        /// </summary>
        private IEnumerator InitializeResources()
        {
            // 1. 获取 Egg 预制体 (沿用 SpawnHelper 的稳健写法)
            if (_eggPrefab == null)
            {
                Egg[] eggs = Resources.FindObjectsOfTypeAll<Egg>();
                if (eggs.Length > 0) _eggPrefab = eggs[0];
            }

            // 2. 获取并筛选适合做“女仆”的预设
            // 我们可以过滤掉 Boss 或测试单位，只保留看起来像人类的
            var allPresets = Resources.FindObjectsOfTypeAll<CharacterRandomPreset>();
            _maidPresets = allPresets
                .Where(p => IsValidMaidPreset(p.name))
                .ToList();

            if (_eggPrefab != null && _maidPresets.Count > 0)
            {
                _isInitialized = true;
                Debug.Log($"[CombatMaid] 初始化完成: 加载了 {_maidPresets.Count} 个可用预设");
            }
            else
            {
                Debug.LogError("[CombatMaid] 初始化失败: 缺少 Egg 或 预设");
            }
            
            yield break;
        }

        /// <summary>
        /// 核心生成方法
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="player">玩家(作为队长)</param>
        /// <param name="config">自定义属性配置</param>
        /// <returns>返回生成的 AI 控制器</returns>
        public AICharacterController SpawnMaid(Vector3 position, CharacterMainControl player, MaidConfig config = null)
        {
            if (!_isInitialized || player == null) return null;

            try
            {
                // 1. 随机选择一个外观预设
                var originalPreset = _maidPresets[Random.Range(0, _maidPresets.Count)];

                // 2. 【关键优化】应用配置：克隆预设并修改属性 (参考 SpawnHelper)
                CharacterRandomPreset finalPreset = ApplyConfigToPreset(originalPreset, config);

                // 3. 生成 Egg
                Egg egg = Instantiate(_eggPrefab, position, Quaternion.identity);
                
                // 4. 初始化 Egg (参数：位置, 朝向, 队长, 预设, 延迟)
                // 注意：这里将 player 传进去，游戏底层可能会自动处理部分同阵营逻辑
                egg.Init(position, player.transform.forward, player, finalPreset, 0.01f);

                Debug.Log($"[CombatMaid] 正在生成: {finalPreset.nameKey} (Team: {finalPreset.team})");

                // 5. 【关键差异】SpawnHelper 生成完就不管了，但我们需要拿到这个 AI
                // 必须立即查找刚刚生成的那个 AI 对象
                return FindJustSpawnedAI(position);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CombatMaid] 生成异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据配置修改预设属性 (Copy from SpawnHelper's logic)
        /// </summary>
        private CharacterRandomPreset ApplyConfigToPreset(CharacterRandomPreset source, MaidConfig config)
        {
            if (config == null) config = new MaidConfig(); // 使用默认值

            // 实例化一个新的预设对象，以免修改到游戏原始资源
            CharacterRandomPreset newPreset = Instantiate(source);

            // 应用数值
            newPreset.health = config.Health;
            newPreset.moveSpeedFactor = config.MoveSpeed;
            newPreset.team = Teams.player; // 强制设为玩家阵营，这很重要！
            
            // 强化感知，防止女仆发呆
            newPreset.sightDistance = 200f; 
            newPreset.hearingAbility = 1.0f;
            newPreset.reactionTime = 0.1f; // 反应极快
            
            // 确保显示名字和血条
            newPreset.showName = true;
            newPreset.showHealthBar = true;

            return newPreset;
        }

        /// <summary>
        /// 查找刚刚生成的 AI (参考 spawn.cs 的逻辑，但更精简)
        /// </summary>
        private AICharacterController FindJustSpawnedAI(Vector3 spawnPos)
        {
            // 因为 Egg.Init 到 AI 生成可能有几毫秒延迟，
            // 这里的同步查找在极少数情况下可能找不到，如果找不到，可以在外部协程里重试
            // 为了保持代码简洁，这里先尝试直接查找
            
            var allAIs = FindObjectsOfType<AICharacterController>();
            AICharacterController bestFit = null;
            float minDistance = SpawnCheckRadius;

            foreach (var ai in allAIs)
            {
                // 排除死人、排除已经注册过的女仆(可以在 Controller 里判断)
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
        /// 简单的预设过滤器
        /// </summary>
        private bool IsValidMaidPreset(string name)
        {
            string n = name.ToLower();
            // 排除掉不想要的东西
            if (n.Contains("boss") || n.Contains("test") || n.Contains("dummy")) return false;
            return true;
        }
        
        // 辅助方法：获取鼠标地面坐标 (直接沿用 SpawnHelper 的优秀实现)
        public Vector3 GetMousePosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Default", "Ground", "Terrain")))
            {
                return hit.point;
            }
            return Vector3.zero;
        }
    }

    /// <summary>
    /// 女仆属性配置类
    /// </summary>
    [System.Serializable]
    public class MaidConfig
    {
        public float Health = 200f;      // 比普通 Scav 血厚一点
        public float MoveSpeed = 1.2f;   // 跑得快一点，才能跟上玩家
        public float DamageMult = 1.0f;
    }
}