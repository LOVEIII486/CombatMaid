using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using MiniLocalizor; // 必须引用，用于解析 DataEntry
using SodaCraft.Localizations; // 引用游戏核心命名空间

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

        // 缓存已加载的语言提供者
        private static readonly Dictionary<SystemLanguage, CSVFileLocalizor> LoadedProviders = new Dictionary<SystemLanguage, CSVFileLocalizor>();
        
        private static SystemLanguage _currentLanguage;
        private static CSVFileLocalizor _currentProvider;
        private static string _modDirectory;
        private static bool _isInitialized = false;

        // 反射缓存：CSVFileLocalizor 的内部字典字段
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
            
            // 缓存反射字段信息，避免重复查找
            _csvDicField = typeof(CSVFileLocalizor).GetField("dic", BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                DetermineCurrentLanguage();
                
                // 1. 尝试加载当前语言
                if (!LoadAndSetLanguage(_currentLanguage))
                {
                    CMDebug.LogWarning($"无法加载语言 {_currentLanguage}，尝试后备语言...");
                    
                    // 2. 失败则尝试英文
                    if (!LoadAndSetLanguage(SystemLanguage.English))
                    {
                        // 3. 最后尝试中文保底
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

                // 清除缓存的 Provider，强制重新读取文件
                LoadedProviders.Clear();
                _currentProvider = null;

                DetermineCurrentLanguage();

                if (LoadAndSetLanguage(_currentLanguage))
                {
                    CMDebug.Log($"语言已刷新并重新注入: {_currentLanguage}");
                }
                else
                {
                    // 尝试回滚到英文
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
        /// 调试热重载检测，建议在 ModBehaviour.Update 中调用
        /// </summary>
        public static void HotReloadLocalization()
        {
            // 按下 F9 进行重载
            if (Input.GetKeyDown(KeyCode.F9))
            {
                CMDebug.Log("[Debug] 检测到 F9 按下，正在重载本地化文件...");
                Refresh();
            }
        }

        // ==================== 公共接口 ====================

        public static string GetText(string key, string fallback = null)
        {
            if (!_isInitialized || _currentProvider == null)
            {
                return fallback ?? key;
            }

            // CSVFileLocalizor.Get 会处理转义字符
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
                // 中文特殊回退逻辑
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
            
            // 检查游戏核心字典是否可用
            if (SodaCraft.Localizations.LocalizationManager.overrideTexts == null)
            {
                CMDebug.LogError("SodaCraft.Localizations.LocalizationManager.overrideTexts 为 null，无法注入文本！");
                return;
            }

            if (_csvDicField == null)
            {
                CMDebug.LogError("反射字段未获取，无法读取 CSVFileLocalizor 字典。");
                return;
            }

            try
            {
                // 反射获取内部字典
                var internalDic = _csvDicField.GetValue(provider) as Dictionary<string, DataEntry>;
                
                if (internalDic == null) return;

                int count = 0;
                var targetDict = SodaCraft.Localizations.LocalizationManager.overrideTexts;

                foreach (var kvp in internalDic)
                {
                    string key = kvp.Key;
                    
                    // 筛选前缀：Item_ 或 Perk_
                    bool match = false;
                    for (int i = 0; i < AutoRegisterPrefixes.Length; i++)
                    {
                        if (key.StartsWith(AutoRegisterPrefixes[i]))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (match)
                    {
                        // 使用 provider.Get 确保获取到处理过转义符的正确文本
                        string value = provider.Get(key); 
                        
                        // 注入到游戏字典
                        targetDict[key] = value;
                        count++;
                    }
                }

                CMDebug.LogInfo($"[Localization] 已注入 {count} 个条目到游戏核心 (Item_/Perk_)");
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"注入文本时发生错误: {ex.Message}");
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
                    return "ChineseTraditional.csv";
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