using System;
using CombatMaid.Core;
using CombatMaid.Core.Items.DebugTools;
using CombatMaid.Core.Items.Logic;
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

            // --- 修改部分开始 ---
            // 使用新的 Registry 初始化物品
            MaidItemRegistry.Initialize(ModRootPath);

            // 挂载调试脚本 (仅在 Debug 模式或即使发布也保留作为彩蛋)
            // 你也可以加个 Config 判断 if (DebugMode) ...
            if (gameObject.GetComponent<ItemDebugSpawner>() == null)
            {
                gameObject.AddComponent<ItemDebugSpawner>();
            }
            // --- 修改部分结束 ---

            InitializeMaidSystem();

            if (ModSettingAPI.Init(info))
            {
                Settings.CombatMaidConfig.Load();
                Settings.SettingsUI.Register(); 
            }
            else
            {
                CMDebug.LogWarning("ModSettingAPI 初始化失败");
            }
        }

        private void OnDisable()
        {
            CleanupLocalization();
            CleanupSceneHooks();
            CleanupHarmonyPatches();
            CleanupMaidSystem();
            
            // 可选：如果需要在禁用时卸载物品，可以调用 FML 的 UnregisterAllItem
            ItemUtils.UnregisterAllItem("CombatMaid");

            Instance = null;
            CMDebug.LogInfo($"模组已禁用");
        }

        #region Maid System

        private void InitializeMaidSystem()
        {
            // 确保核心管理器单例存在
            if (MaidManager.Instance == null)
            {
                var go = new GameObject("MaidManager");
                
                // 1. 挂载管理器
                go.AddComponent<MaidManager>();
                
                // 2. [新增关键修复] 挂载生成器！没有它就无法生成实体
                go.AddComponent<MaidSpawner>(); 
                
                DontDestroyOnLoad(go);
                CMDebug.Log("女仆系统 (Manager + Spawner) 初始化完成。");
            }
        }

        private void CleanupMaidSystem()
        {
            var manager = gameObject.GetComponent<MaidManager>();
            if (manager != null)
            {
                Destroy(manager);
            }
            var spawner = gameObject.GetComponent<MaidSpawner>();
            if (spawner != null)
            {
                Destroy(spawner);
            }

            CMDebug.LogInfo($"女仆核心系统已卸载");
        }

        #endregion
        
        #region Localization Management

        private void InitializeLocalization()
        {
            LocalizationManager.Initialize(info.path);
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage += OnLanguageChanged;
            CMDebug.LogInfo($"本地化系统已挂载");
        }

        private void CleanupLocalization()
        {
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage -= OnLanguageChanged;
            LocalizationManager.Cleanup();
        }

        private void OnLanguageChanged(SystemLanguage lang)
        {
            // 1. 刷新 CSV 读取器
            LocalizationManager.Refresh();
            
            // 2. [新增] 将新读取到的文本重新注入到游戏系统
            MaidItemRegistry.RefreshLocalizations();
            
            // 3. 刷新设置界面
            Settings.SettingsUI.Register();
            
            CMDebug.LogInfo($"语言已切换为 {lang}，物品文本已更新。");
        }

        #endregion

        #region Harmony Management

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
        
        private void OnDestroy()
        {
            CleanupHarmonyPatches();
            CleanupSceneHooks();
            
            // --- 新增清理 ---
            MaidItemRegistry.Cleanup();
            // ----------------
            
            Instance = null;
        }
    }
}