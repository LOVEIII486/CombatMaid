using System;
using HarmonyLib;
using Duckov.Modding;
using CombatMaid.Localization;
using CombatMaid.ModSettingsApi;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatMaid
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        public static ModBehaviour Instance { get; private set; }
        
        private const string HarmonyId = "com.LOVEIII486.CombatMaid"; 
        private const string LogTag = "[CombatMaid]";
        
        private Harmony _harmony;
        private bool _isPatched = false;
        private bool _sceneHooksInitialized = false;

        private void OnEnable()
        {
            Instance = this;
            
            if (HarmonyLoad.LoadHarmony() == null)
            {
                Debug.LogError($"{LogTag} 模组启动失败: 缺少 Harmony 依赖。");
                return;
            }
            
            InitializeHarmonyPatches();
            InitializeSceneHooks();
            
            Debug.Log($"{LogTag} 模组已启用");
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
                Debug.LogError($"{LogTag} ModSetting 依赖缺失或初始化失败！");
            }
        }

        private void OnDisable()
        {
            CleanupLocalization();
            CleanupSceneHooks();
            CleanupHarmonyPatches();

            Instance = null;
            Debug.Log($"{LogTag} 模组已禁用");
        }

        #region Localization Management

        private void InitializeLocalization()
        {
            LocalizationManager.Initialize(info.path);
            SodaCraft.Localizations.LocalizationManager.OnSetLanguage += OnLanguageChanged;
            Debug.Log($"{LogTag} 本地化系统已挂载");
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
                Debug.Log($"{LogTag} Harmony 补丁应用成功");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} Harmony 补丁应用失败: {ex}");
            }
        }

        private void CleanupHarmonyPatches()
        {
            if (!_isPatched || _harmony == null) return;

            try
            {
                _harmony.UnpatchAll(_harmony.Id);
                _isPatched = false;
                Debug.Log($"{LogTag} Harmony 补丁已移除");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} 移除 Harmony 补丁时发生错误: {ex}");
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
            Debug.Log($"{LogTag} 进入场景: {scene.name}");
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Debug.Log($"{LogTag} 场景卸载: {scene.name}");
        }

        #endregion
    }
}