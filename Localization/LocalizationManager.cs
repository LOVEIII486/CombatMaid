using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using MiniLocalizor;

namespace CombatMaid.Localization
{
    /// <summary>
    /// 本地化管理器
    /// 负责加载模组语言文件，并将其注入到游戏的 LocalizationManager 中
    /// </summary>
    public static class LocalizationManager
    {
        private const string LocalizationFolderName = "Localization";
        
        // 需要自动注册到游戏核心的 Key 前缀
        private static readonly string[] AutoRegisterPrefixes = { "Item_", "Perk_","SkillTree_","Buff_MaidBuff_"};

        private static readonly Dictionary<SystemLanguage, CSVFileLocalizor> LoadedProviders = new Dictionary<SystemLanguage, CSVFileLocalizor>();
        
        private static SystemLanguage _currentLanguage;
        private static CSVFileLocalizor _currentProvider;
        private static string _modDirectory;
        private static bool _isInitialized = false;

        private static FieldInfo _csvDicField;

        // ==================== 初始化与生命周期 ====================

        public static void Initialize(string modDirectory)
        {
            if (_isInitialized)
            {
                CMDebug.LogWarning("本地化系统已初始化，跳过重复初始化");
                return;
            }

            _modDirectory = modDirectory;
            
            _csvDicField = typeof(CSVFileLocalizor).GetField("dic", BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                DetermineCurrentLanguage();
                
                if (!LoadAndSetLanguage(_currentLanguage))
                {
                    CMDebug.LogWarning($"无法加载语言 {_currentLanguage}，尝试后备语言...");
                    
                    if (!LoadAndSetLanguage(SystemLanguage.English))
                    {
                        if (!LoadAndSetLanguage(SystemLanguage.ChineseSimplified) &&
                            !LoadAndSetLanguage(SystemLanguage.Chinese))
                        {
                            CMDebug.LogError("严重错误：无法加载任何语言文件（English/Chinese）！");
                        }
                    }
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"本地化初始化失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 刷新/热重载本地化数据
        /// </summary>
        public static void Refresh()
        {
            if (!_isInitialized)
            {
                CMDebug.LogWarning("本地化系统尚未初始化，无法刷新");
                return;
            }

            try
            {
                CMDebug.Log("开始刷新本地化系统...");

                LoadedProviders.Clear();
                _currentProvider = null;

                DetermineCurrentLanguage();

                if (LoadAndSetLanguage(_currentLanguage))
                {
                    CMDebug.Log($"语言已刷新并重新注入: {_currentLanguage}");
                }
                else
                {
                    CMDebug.LogWarning($"刷新后加载 {_currentLanguage} 失败，尝试使用 English");
                    LoadAndSetLanguage(SystemLanguage.English);
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"刷新失败: {ex.Message}");
            }
        }

        public static void Cleanup()
        {
            LoadedProviders.Clear();
            _currentProvider = null;
            _modDirectory = null;
            _isInitialized = false;
        }

        // ==================== 调试功能 ====================

        /// <summary>
        /// 调试热重载检测
        /// </summary>
        public static void HotReloadLocalization()
        {
            CMDebug.Log("正在重载本地化文件...");
            Refresh();
        }

        // ==================== 公共接口 ====================

        public static string GetText(string key, string fallback = null)
        {
            if (!_isInitialized || _currentProvider == null)
            {
                return fallback ?? key;
            }

            string val = _currentProvider.Get(key);
            if (val != null)
            {
                return val;
            }

            return fallback ?? key;
        }

        // ==================== 内部逻辑 ====================

        private static void DetermineCurrentLanguage()
        {
            _currentLanguage = SodaCraft.Localizations.LocalizationManager.Initialized
                ? SodaCraft.Localizations.LocalizationManager.CurrentLanguage
                : Application.systemLanguage;
        }

        private static bool LoadAndSetLanguage(SystemLanguage language)
        {
            // 1. 检查缓存
            if (LoadedProviders.TryGetValue(language, out var cachedProvider))
            {
                _currentProvider = cachedProvider;
                // 即使是缓存，切换语言时也重新注入一次，防止被覆盖
                InjectToGameManager(cachedProvider);
                return true;
            }

            // 2. 构建路径
            string fileName = GetLanguageFileName(language);
            string filePath = Path.Combine(_modDirectory, LocalizationFolderName, fileName);

            if (!File.Exists(filePath))
            {
                if (language == SystemLanguage.Chinese)
                {
                    string simPath = Path.Combine(_modDirectory, LocalizationFolderName, "ChineseSimplified.csv");
                    if (File.Exists(simPath)) filePath = simPath;
                    else return false;
                }
                else
                {
                    return false;
                }
            }

            // 3. 加载文件
            try
            {
                var provider = new CSVFileLocalizor(filePath);
                
                // 存入缓存
                LoadedProviders[language] = provider;
                _currentProvider = provider;
                
                CMDebug.LogInfo($"已加载语言文件: {fileName}");

                // 4. 注入到游戏核心
                InjectToGameManager(provider);

                return true;
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"加载文件异常 {fileName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将当前 Provider 中的特定前缀条目注入到游戏核心管理器
        /// </summary>
        private static void InjectToGameManager(CSVFileLocalizor provider)
        {
            if (provider == null) return;
            if (_csvDicField == null)
            {
                CMDebug.LogError("反射字段 _csvDicField 未初始化，无法注入文本");
                return;
            }

            try
            {
                var internalDic = _csvDicField.GetValue(provider) as Dictionary<string, DataEntry>;
                if (internalDic == null)
                {
                    CMDebug.LogWarning("无法从 provider 获取有效的内部字典数据");
                    return;
                }

                int count = 0;

                foreach (var kvp in internalDic)
                {
                    string key = kvp.Key;
                    if (string.IsNullOrEmpty(key)) continue;

                    // 筛选匹配前缀的条目
                    bool match = false;
                    foreach (var prefix in AutoRegisterPrefixes)
                    {
                        if (key.StartsWith(prefix))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (match)
                    {
                        string value = provider.Get(key);
                        SodaCraft.Localizations.LocalizationManager.SetOverrideText(key, value);
                        count++;
                    }
                }

                if (count > 0)
                {
                    CMDebug.LogInfo($"注入了 {count} 个本地化条目。");
                }
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"注入文本时发生错误: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private static string GetLanguageFileName(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return "ChineseSimplified.csv";
                case SystemLanguage.ChineseTraditional:
                    return "ChineseSimplified.csv"; // 临时使用中文简体
                case SystemLanguage.English:
                    return "English.csv";
                case SystemLanguage.Japanese:
                    return "Japanese.csv";
                case SystemLanguage.Korean:
                    return "Korean.csv";
                case SystemLanguage.Russian:
                    return "Russian.csv";
                default:
                    return $"{language}.csv";
            }
        }
    }
}