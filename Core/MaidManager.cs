using System.Collections.Generic;
using CombatMaid.Core.SkillTreeSystem;
using UnityEngine;
using Duckov.Modding;
using CombatMaid.Core.WineFox;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        #region Singleton & Lifecycle

        public static MaidManager Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                _enemyLayerMask = LayerMask.GetMask("DamageReceiver", "Character", "HeadCollider");

                CMDebug.LogInfo("MaidManager (指挥官) 初始化完成。");
            }
            else
            {
                Destroy(this);
            }
        }

        private void Update()
        {
            
#if COMBATMAID_DEBUG
            HandleDebugInput();
#else
            //HandleDebugInput();
#endif
            HandleCommandInput();
            UpdateFocusTarget();
        }

        public void OnLevelStart(string sceneName)
        {
        }

        public void OnLevelEnd()
        {
            DespawnTeam();
        }

        #endregion

        #region Team Management

        // 只管理活跃单位
        private List<MaidController> _activeMaids = new List<MaidController>();

        /// <summary>
        /// [新接口] 供 Spawner 调用，注册新生成的女仆入队
        /// </summary>
        public void RegisterActiveMaid(MaidController maid)
        {
            if (maid != null && !_activeMaids.Contains(maid))
            {
                _activeMaids.Add(maid);
                CMDebug.Log($"[Manager] 女仆归队: {maid.name} (当前队伍: {_activeMaids.Count})");
            }
        }

        /// <summary>
        /// 解散队伍 (销毁所有女仆)
        /// </summary>
        public void DespawnTeam()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    if (maid.MaidCharacter != null) Destroy(maid.MaidCharacter.gameObject);
                    else if (maid.gameObject != null) Destroy(maid.gameObject);
                }
            }

            _activeMaids.Clear();
            CMDebug.Log("女仆队伍已解散");
        }

        /// <summary>
        /// 获取活跃的酒狐实例 (用于契约检测)
        /// </summary>
        public MaidController GetActiveWineFox()
        {
            // 倒序遍历以安全移除空引用
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];

                // 1. 清理无效引用
                if (maid == null || maid.gameObject == null)
                {
                    _activeMaids.RemoveAt(i);
                    continue;
                }

                // 2. 检查标记组件 (WineFoxDataSync 仅挂载在酒狐身上)
                if (maid.GetComponent<WineFoxDataSync>() != null)
                {
                    return maid;
                }
            }

            return null;
        }

        #endregion

        #region Command System
        
        // 集火系统变量
        public CharacterMainControl FocusTarget { get; private set; }
        private float _focusExpireTimer = 0f;
        private const float FocusDuration = 8.0f;
        private const float RaycastDistance = 150f; 
        private int _enemyLayerMask;

        private void HandleCommandInput()
        {
            if (Input.GetKeyDown(Settings.CombatMaidConfig.KeyMove))
            {
                CommandMoveTeamToMouse();
            }

            if (Input.GetKeyDown(Settings.CombatMaidConfig.KeyHeal))
            {
                CommandForceHealTeam();
            }

            if (Input.GetKeyDown(Settings.CombatMaidConfig.KeyHold))
            {
                CommandToggleHoldTeam();
            }
            
            if (Input.GetKeyDown(Settings.CombatMaidConfig.KeyScavenge))
            {
                CommandToggleScavenge();
            }
            
            if (Input.GetKeyDown(Settings.CombatMaidConfig.KeyDrop))
            {
                CommandDropItems();
            }
            
            if (Input.GetKeyDown(Settings.CombatMaidConfig.KeyInventoryManage))
            {
                CommandInventoryManage_WineFox();
            }
            
            if (Input.GetKeyDown(Settings.CombatMaidConfig.KeyPassive))
            {
                CommandTogglePassiveFollow();
            }
        }

        private void UpdateFocusTarget()
        {
            if (_focusExpireTimer > 0)
            {
                _focusExpireTimer -= Time.deltaTime;
                if (_focusExpireTimer <= 0)
                {
                    FocusTarget = null;
                }
            }
            if (Input.GetMouseButton(0)) // 按住或点击均可
            {
                DetectPlayerTarget();
            }
            if (FocusTarget != null && (FocusTarget.Health == null || FocusTarget.Health.IsDead))
            {
                FocusTarget = null;
            }
        }

        private void DetectPlayerTarget()
        {
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, RaycastDistance, _enemyLayerMask))
            {
                var target = hit.collider.GetComponentInParent<CharacterMainControl>();
                
                if (target == null || target.Health.IsDead || target.Team == CharacterMainControl.Main.Team) 
                    return;

                if (FocusTarget != target)
                {
                    FocusTarget = target;
                    _focusExpireTimer = FocusDuration;
                    // if (CharacterMainControl.Main != null)
                    // {
                    //     CharacterMainControl.Main.PopText($">>> 集火: {target.name} <<<");
                    // }
                    // CMDebug.Log($"[集火] 锁定目标: {target.name} (距离: {hit.distance:F1}m)");
                }
                else
                {
                    // 持续按住时刷新计时器
                    _focusExpireTimer = FocusDuration;
                }
            }
        }

        private void CommandToggleHoldTeam()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    maid.ToggleHoldPosition();
                }
            }
        }

        private void CommandMoveTeamToMouse()
        {
            Vector3 targetPos = GetMousePosition();
            if (targetPos == Vector3.zero) return;

            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
                    maid.ForceMoveTo(targetPos + offset);
                    maid.MaidCharacter.PopText("战术移动！");
                }
            }
        }

        private void CommandForceHealTeam()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                maid.ForceHeal();
                CharacterMainControl.Main.PopText("手动治疗！");
            }
        }
        
        private void CommandToggleScavenge()
        {
            foreach (var maid in _activeMaids)
            {
                if (maid != null) maid.CommandScavenge();
            }
        }
        
        private void CommandDropItems()
        {
            foreach (var maid in _activeMaids)
            {
                if (maid != null)
                {
                    maid.CommandDumpLoot();
                }
            }
        }

        private void CommandInventoryManage_WineFox()
        {
                var wineFox = GetActiveWineFox();
                if (wineFox != null)
                {
                    wineFox.ToggleInventoryManagement();
                }
                else
                {
                    CMDebug.LogWarning("未找到酒狐，无法打开背包。");
                }
        }
        
        private void CommandTogglePassiveFollow()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid != null)
                {
                    maid.TogglePassiveFollow();
                }
            }
        }

        private Vector3 GetMousePosition()
        {
            if (Camera.main == null) return Vector3.zero;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
    
            LayerMask mask = LayerMask.GetMask(
                "Default",          // Layer 0: 默认层，通常包含大部分未分类的静态物体
                "Wall",             // Layer 6: 墙壁，核心障碍物
                "Ground",           // Layer 7: 地面，核心行走层
                "Interactable",     // Layer 8: 交互物（如箱子、工作台），防止点到箱子后面去
                "Door",             // Layer 13: 门，防止穿门
                "HalfObsticle",     // Layer 14: 半身掩体（注意日志里的拼写是 Obsticle）
                "Wall_FowBlock"     // Layer 19: 战争迷雾阻挡墙，通常也是实体墙
            );

            float maxDistFromPlayer = 25.0f; // 最大指挥距离
            float maxRayDist = 100f;       // 射线最大检测距离
            Vector3 rawTargetPos;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDist, mask))
            {
                rawTargetPos = hit.point;
            }
            else
            {
                rawTargetPos = ray.GetPoint(50.0f);
            }

            Vector3 playerPos = CharacterMainControl.Main.transform.position;
            Vector3 toTarget = rawTargetPos - playerPos;
    
            Vector3 flatDirection = new Vector3(toTarget.x, 0, toTarget.z).normalized;
            float currentDist = new Vector3(toTarget.x, 0, toTarget.z).magnitude;

            if (currentDist > maxDistFromPlayer)
            {
                rawTargetPos = playerPos + flatDirection * maxDistFromPlayer;
            }

            float castHeight = Mathf.Max(playerPos.y, rawTargetPos.y) + 5.0f;
            Vector3 castOrigin = new Vector3(rawTargetPos.x, castHeight, rawTargetPos.z);

            if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit groundHit, 20.0f, mask))
            {
                return groundHit.point;
            }
            return Vector3.zero;
        }

        #endregion

        #region Debug Input
        
        private void HandleDebugInput()
        {
            // F5 测试: 调用 Spawner 生成酒狐
            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (MaidSpawner.Instance != null && CharacterMainControl.Main != null)
                {
                    MaidSpawner.Instance.SpawnWineFox(CharacterMainControl.Main.transform.position);
                }
            }

            // F6 清除
            if (Input.GetKeyDown(KeyCode.F6)) DespawnTeam();
            
            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (SkillTreeManager.Instance != null)
                {
                    SkillTreeManager.Instance.ReloadTree();
            
                    // 可选：给玩家发个提示
                    var player = CharacterMainControl.Main;
                    if (player != null) player.PopText("正在重载技能树...");
                    
                    CombatMaid.Localization.LocalizationManager.HotReloadLocalization();
                }
            }
            
            // F8 重载配置: 调用 Spawner
            if (Input.GetKeyDown(KeyCode.F8))
            {
                MaidSpawner.Instance?.LoadAllCustomPresets();
            }

            // F9 调试输出
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (MaidSpawner.Instance != null)
                {
                    MaidSpawner.Instance.DebugListAllKeys();
                    MaidSpawner.Instance.DebugExportReferenceStats();
                }
            }
            
            if (Input.GetKeyDown(KeyCode.F2))
            {
                foreach (var maid in _activeMaids)
                {
                    if (maid != null)
                    {
                        maid.DebugPrintMaidStatus();
                    }
                }
            }
            
        }

        #endregion
    }
}