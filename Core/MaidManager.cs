using System.Collections.Generic;
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

                _enemyLayerMask = LayerMask.GetMask("Default", "Character", "Hitbox", "Enemy");

                CMDebug.LogInfo("MaidManager (指挥官) 初始化完成。");
            }
            else
            {
                Destroy(this);
            }
        }

        private void Update()
        {
            HandleDebugInput();
            HandleCommandInput();
            UpdateFocusTarget();
        }

        public void OnLevelStart(string sceneName) { }

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

        public CharacterMainControl FocusTarget { get; private set; }
        private float _focusExpireTimer = 0f;
        private const float FocusDuration = 5.0f;
        private const float RaycastDistance = 25f;
        private int _enemyLayerMask;

        private void HandleCommandInput()
        {   
            if (Input.GetKeyDown(KeyCode.F)) CommandToggleHoldTeam();
            
            // G 移动指令
            if (Input.GetKeyDown(KeyCode.G)) CommandMoveTeamToMouse();

            // H 强制回血
            if (Input.GetKeyDown(KeyCode.H)) CommandForceHealTeam();
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

            if (Input.GetMouseButton(0)) DetectPlayerTarget();

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
                if (target != null && !target.Health.IsDead && target.Team != Teams.player)
                {
                    if (FocusTarget != target)
                    {
                        FocusTarget = target;
                        CMDebug.Log($"[指令] 集火目标: {target.name}");
                    }

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
            CMDebug.Log("[指令] 强制全队尝试使用医疗包 (H)");
            for (int i = _activeMaids.Count - 1; i >= 0; i--)
            {
                var maid = _activeMaids[i];
                maid.ForceHeal();
                maid.MaidCharacter.PopText("手动治疗！");
            }
        }

        private Vector3 GetMousePosition()
        {
            if (Camera.main == null) return Vector3.zero;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMask.GetMask("Default", "Ground", "Terrain")))
                return hit.point;
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
        }

        #endregion
    }
}