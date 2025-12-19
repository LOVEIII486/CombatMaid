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

        #region 女仆注册管理

        private List<MaidController> _activeMaids = new List<MaidController>();
        public int ActiveMaidCount => _activeMaids.Count;
        public bool HasActiveMaids => _activeMaids.Count > 0;

        /// <summary>
        /// 注册新生成的女仆入队
        /// </summary>
        public void RegisterActiveMaid(MaidController maid)
        {
            if (maid != null && !_activeMaids.Contains(maid))
            {
                _activeMaids.Add(maid);
                CMDebug.Log($"女仆归队: {maid.MaidCharacter.characterPreset.DisplayName} (当前队伍: {_activeMaids.Count})");
            }
        }

        /// <summary>
        /// 解散队伍
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
        /// 获取活跃的酒狐实例
        /// </summary>
        public MaidController GetActiveWineFox()
        {
            // 倒序遍历以安全移除空引用
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];

                if (maid == null || maid.gameObject == null)
                {
                    _activeMaids.RemoveAt(i);
                    continue;
                }

                // 检查标记组件 (WineFoxDataSync 仅挂载在酒狐身上)
                if (maid.GetComponent<WineFoxDataSync>() != null)
                {
                    return maid;
                }
            }

            return null;
        }

        public List<MaidController> GetAllActiveMaids()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                if (maid == null || maid.gameObject == null)
                {
                    _activeMaids.RemoveAt(i);
                }
            }

            // 返回当前列表的副本，防止外部直接修改私有列表
            return new List<MaidController>(_activeMaids);
        }

        #endregion

        #region 指令

        // 集火系统变量
        public CharacterMainControl FocusTarget { get; private set; }
        private float _focusExpireTimer = 0f;
        private const float FocusDuration = 5.0f;
        private const float RaycastDistance = 150f;
        private int _enemyLayerMask;
        private static readonly int CommandMask = LayerMask.GetMask("Default", "Wall", "Ground", "Interactable", "Door", "HalfObsticle", "Wall_FowBlock");
        
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
    
            if (!Physics.Raycast(ray, out RaycastHit eyeHit, 100f, CommandMask)) 
                return Vector3.zero;

            Vector3 targetPos = eyeHit.point;
            Vector3 playerPos = CharacterMainControl.Main.transform.position;

            // 2. 距离钳制：使用平面距离判断
            Vector3 diff = targetPos - playerPos;
            float flatDistSqr = diff.x * diff.x + diff.z * diff.z; // 使用平方根比较性能更好
            if (flatDistSqr > 900f) // 30.0f * 30.0f
            {
                float ratio = 30.0f / Mathf.Sqrt(flatDistSqr);
                targetPos = playerPos + new Vector3(diff.x * ratio, diff.y, diff.z * ratio);
            }

            // 3. 穿透逻辑：从目标点下方 0.2米 开始向下探测（直接跳过当前的屋顶/表面）
            // 这种方法性能最高，完全不需要 RaycastAll 和 排序
            if (Physics.Raycast(targetPos + Vector3.down * 0.2f, Vector3.down, out RaycastHit floorHit, 20f, CommandMask))
            {
                return floorHit.point;
            }

            // 如果下方没东西（比如点了地图边缘或纯空地），返回第一射的点
            return targetPos;
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

                    
                }
            }
            
            // F8 重载本地化
            if (Input.GetKeyDown(KeyCode.F8))
            {
                CombatMaid.Localization.LocalizationManager.HotReloadLocalization();
            }

            // // F8 重载配置: 调用 Spawner
            // if (Input.GetKeyDown(KeyCode.F8))
            // {
            //     MaidSpawner.Instance?.LoadAllCustomPresets();
            // }

            // F9 调试输出
            if (Input.GetKeyDown(KeyCode.F9))
            {
                if (MaidSpawner.Instance != null)
                {
                    MaidSpawner.Instance.DebugListAllKeys();
                    MaidSpawner.Instance.DebugExportReferenceStats();
                }
            }
        }

        #endregion
    }
}