using System;
using System.Collections.Generic;
using System.Linq;
using CombatMaid.Core.MaidEventSystem;
using CombatMaid.Core.SkillTreeSystem;
using UnityEngine;
using CombatMaid.Core.WineFox;
using CombatMaid.Localization;
using Duckov.UI;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

namespace CombatMaid.Core
{
    public class MaidManager : MonoBehaviour
    {
        #region 基础

        public static MaidManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                _enemyLayerMask = LayerMask.GetMask("DamageReceiver", "Character", "HeadCollider");

                CMDebug.LogInfo("MaidManager 初始化完成。");
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
                MaidEventManager.OnMaidJoinTeam(maid);
                CMDebug.Log($"女仆归队: {maid.MaidCharacter.characterPreset.nameKey} (当前队伍: {_activeMaids.Count})");
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

        private static readonly int CommandMask = LayerMask.GetMask("Default", "Wall", "Ground", "Interactable", "Door",
            "HalfObsticle", "Wall_FowBlock");

        private readonly Lazy<string> _txtTacticalMove =
            new Lazy<string>(() => LocalizationManager.GetText("Msg_Maid_TacticalMove"));

        private readonly Lazy<string> _txtManualHeal =
            new Lazy<string>(() => LocalizationManager.GetText("Msg_Maid_ManualHeal"));

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
            // 1. 检查是否存在打开的 UI 视图
            if (GameplayUIManager.Instance != null && GameplayUIManager.Instance.ActiveView != null)
            {
                FocusTarget = null;
                _focusExpireTimer = 0;
                return;
            }

            // 2. 检查鼠标是否正悬停在任何 UI 元素上
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // 3. 倒计时
            if (_focusExpireTimer > 0)
            {
                _focusExpireTimer -= Time.deltaTime;
                if (_focusExpireTimer <= 0) FocusTarget = null;
            }

            if (Input.GetMouseButton(0))
            {
                DetectPlayerTarget();
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
                    maid.MaidCharacter.PopText(_txtTacticalMove.Value);
                }
            }
        }

        private void CommandForceHealTeam()
        {
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                maid.ForceHeal();
                CharacterMainControl.Main.PopText(_txtManualHeal.Value);
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
            var hits = Physics.RaycastAll(ray, 150f, CommandMask).OrderBy(h => h.distance).ToArray();
    
            foreach (var hit in hits)
            {
                GameObject go = hit.collider.gameObject;
                string n = go.name.ToLower(), p = go.transform.parent?.name.ToLower() ?? "";

                // 过滤：跳过房顶、毒气区触发器、遮挡渐变体
                if (n.Contains("roof") || p.Contains("roof") || n.Contains("fade") || n.Contains("zone")) continue;
                if (hit.normal.y < 0.7f) continue;

                Vector3 targetPos = hit.point;

                // 限制在 30m 内
                Vector3 offset = targetPos - CharacterMainControl.Main.transform.position;
                if (offset.sqrMagnitude > 900f) targetPos = CharacterMainControl.Main.transform.position + offset.normalized * 30f;

                if (Physics.Raycast(targetPos + Vector3.up * 0.1f, Vector3.down, out var snap, 0.5f, CommandMask)) targetPos = snap.point;

                // CMDebug.Log($"[路径追踪] 锁定地面: {go.name} H: {targetPos.y:F2}");
                return targetPos;
            }

            return Vector3.zero;
        }

        #endregion

        #region Debug指令

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

                    var player = CharacterMainControl.Main;
                    if (player != null) player.PopText("正在重载技能树...");
                }
            }

            // F8 重载本地化
            if (Input.GetKeyDown(KeyCode.F8))
            {
                CombatMaid.Localization.LocalizationManager.HotReloadLocalization();
                var player = CharacterMainControl.Main;
                if (player != null) player.PopText("已重载本地化文件...");
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

                    var player = CharacterMainControl.Main;
                    if (player != null) player.PopText("参考预设key和preset已输出...");
                }
            }
        }

        #endregion
    }
}