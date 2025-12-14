using System;
using CombatMaid.Core;
using CombatMaid.Core.BuffsSystem;
using CombatMaid.Core.Items.Logic;
using CombatMaid.Core.SkillTreeSystem;
using HarmonyLib;
using Duckov.Modding;
using CombatMaid.Localization;
using CombatMaid.ModSettingsApi;
using FastModdingLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatMaid
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static ModBehaviour Instance { get; private set; }
        public string ModRootPath => info.path;
        
        private const string HarmonyId = "com.LOVEIII486.CombatMaid"; 
        
        private Harmony _harmony;
        private bool _isPatched = false;
        private bool _sceneHooksInitialized = false;

        private void OnEnable()
        {
            Instance = this;
            
            if (HarmonyLoad.LoadHarmony() == null)
            {
                CMDebug.LogError($"模组启动失败: 缺少 Harmony 依赖。");
                return;
            }
            
            InitializeHarmonyPatches();
            InitializeSceneHooks();
            
            CMDebug.LogInfo($"模组已启用");
        }
        
        protected override void OnAfterSetup()
        {
            base.OnAfterSetup();
            InitializeLocalization(); 
            if (ModSettingAPI.Init(info))
            {
                Settings.CombatMaidConfig.Load();
                Settings.SettingsUI.Register(); 
            }
            else
            {
                CMDebug.LogWarning("ModSettingAPI 初始化失败");
            }
            InitializeMaidItems();
            InitializeMaidBuffSystem();
            
            InitializeMaidSystem();
            InitializeSkillTreeSystem();
            
            // if (gameObject.GetComponent<CombatMaid.DebugTools.InventoryDebugger>() == null)
            // {
            //     gameObject.AddComponent<CombatMaid.DebugTools.InventoryDebugger>();
            //     CMDebug.LogInfo("EnemyInventoryDebugger 已挂载，按 F6 测试。");
            // }
        }

        private void OnDisable()
        {
            CleanupAllSystems();
            Instance = null;
            CMDebug.LogInfo($"模组已禁用");
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupAllSystems();
                Instance = null;
            }
            CMDebug.LogInfo("模组已完全销毁");
        }

        private void CleanupAllSystems()
        {
            CleanupLocalization();
            CleanupSceneHooks();
            CleanupHarmonyPatches();
            CleanupMaidItems();
            CleanupMaidBuffSystem();
            CleanupMaidSystem();
            CleanupSkillTreeSystem();
        }

        #region MaidItem

        private void InitializeMaidItems()
        {
            MaidItemRegistry.Initialize(ModRootPath);
        }
        
        private void CleanupMaidItems()
        {
            MaidItemRegistry.Cleanup();
        }

        #endregion
        
        #region MaidSystem

        private void InitializeMaidSystem()
        {
            if (MaidManager.Instance == null)
            {
                var go = new GameObject("MaidManager");
                go.AddComponent<MaidManager>();
                go.AddComponent<MaidSpawner>(); 
                
                DontDestroyOnLoad(go);
                CMDebug.Log("女仆系统初始化完成。");
            }
        }

        private void CleanupMaidSystem()
        {
            if (MaidManager.Instance != null)
            {
                Destroy(MaidManager.Instance.gameObject);
            }

            CMDebug.LogInfo($"女仆系统已卸载");
        }

        #endregion
        
        #region MaidBuffSystem (新增部分)

        private void InitializeMaidBuffSystem()
        {
            MaidBuffRegistry.Instance.Initialize();
        }

        private void CleanupMaidBuffSystem()
        {
            if (MaidBuffRegistry.Instance != null)
            {
                MaidBuffRegistry.Instance.Cleanup();
            }

            // 清理运行时修改器
            if (MaidBuffModifierManager.Instance != null)
            {
                MaidBuffModifierManager.Instance.Clear();
            }
        }

        #endregion
        
        #region SkillTreeSystem

        private void InitializeSkillTreeSystem()
        {
            if (SkillTreeManager.Instance == null)
            {
                var go = new GameObject("CM_SkillTreeManager");
                go.AddComponent<SkillTreeManager>();
                
                DontDestroyOnLoad(go);
                CMDebug.Log("技能树系统已初始化。");
            }
        }

        private void CleanupSkillTreeSystem()
        {
            if (SkillTreeManager.Instance != null)
            {
                Destroy(SkillTreeManager.Instance.gameObject);
            }

            CMDebug.LogInfo($"技能树系统已卸载");
        }

        #endregion
        
        #region Localization

        private void InitializeLocalization()
        {
            LocalizationManager.Initialize(info.path);
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage += OnLanguageChanged;
        }

        private void CleanupLocalization()
        {
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage -= OnLanguageChanged;
            LocalizationManager.Cleanup();
        }

        private void OnLanguageChanged(SystemLanguage lang)
        {
            LocalizationManager.Refresh();
            Settings.SettingsUI.Register();
        }

        #endregion

        #region HarmonyPatches

        private void InitializeHarmonyPatches()
        {
            if (_isPatched) return;
            
            try
            {
                if (_harmony == null)
                {
                    _harmony = new Harmony(HarmonyId);
                }
                _harmony.PatchAll();
                _isPatched = true;
                CMDebug.LogInfo($"Harmony 补丁应用成功");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"Harmony 补丁应用失败: {ex}");
            }
        }

        private void CleanupHarmonyPatches()
        {
            if (!_isPatched || _harmony == null) return;

            try
            {
                _harmony.UnpatchAll(_harmony.Id);
                _isPatched = false;
                CMDebug.LogInfo($"Harmony 补丁已移除");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"移除 Harmony 补丁时发生错误: {ex}");
            }
        }

        #endregion

        #region Scene Hooks

        private void InitializeSceneHooks()
        {
            if (_sceneHooksInitialized) return;
            
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            
            _sceneHooksInitialized = true;
        }

        private void CleanupSceneHooks()
        {
            if (!_sceneHooksInitialized) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _sceneHooksInitialized = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CMDebug.Log($"进入场景: {scene.name}");

            if (MaidManager.Instance != null)
            {
                MaidManager.Instance.OnLevelStart(scene.name);
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            CMDebug.Log($"场景卸载: {scene.name}");

            if (MaidManager.Instance != null)
            {
                MaidManager.Instance.OnLevelEnd();
            }
        }

        #endregion
        
    }
}