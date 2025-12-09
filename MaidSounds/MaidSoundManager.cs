using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FMOD;
using CombatMaid.Core; 

namespace CombatMaid.MaidSounds
{
    /// <summary>
    /// 管理自定义女仆语音的逻辑核心 (带缓存优化版)
    /// </summary>
    public static class MaidSoundManager
    {
        // 硬编码支持替换的 soundKey 列表
        private static readonly HashSet<string> SupportedKeys = new HashSet<string>
        {
            "surprise"
        };

        // 缓存字典：Key = 声音类型(如 "surprise"), Value = 该类型下所有可用的文件路径列表
        private static Dictionary<string, List<string>> _soundCache = new Dictionary<string, List<string>>();

        /// <summary>
        /// 判断是否应该拦截该声音
        /// </summary>
        public static bool ShouldReplace(GameObject source, string soundKey)
        {
            if (source == null || string.IsNullOrEmpty(soundKey)) return false;
            // 忽略大小写检查 key
            if (!SupportedKeys.Contains(soundKey.ToLowerInvariant())) return false;
            if (source.GetComponent<MaidController>() == null) return false;
            return true;
        }

        /// <summary>
        /// 尝试播放自定义声音 (带缓存 & 随机变体)
        /// </summary>
        public static bool TryPlayMaidSound(GameObject source, string soundKey)
        {
            try
            {
                // 统一转换为小写作为 Cache Key，防止大小写不一致导致缓存失效
                string cacheKey = soundKey.ToLowerInvariant();
                List<string> candidates;

                // 1. 检查缓存中是否有该声音的文件列表
                if (!_soundCache.TryGetValue(cacheKey, out candidates))
                {
                    // === 缓存未命中，执行磁盘扫描 ===
                    candidates = new List<string>();
                    string modPath = ModBehaviour.Instance.ModRootPath;
                    string soundFolder = Path.Combine(modPath, "MaidSounds");

                    if (Directory.Exists(soundFolder))
                    {
                        // 查找模式：{soundKey}_maid*.mp3
                        // 例如: "surprise" -> 扫描 "surprise_maid*.mp3"
                        string searchPattern = $"{cacheKey}_maid*.mp3"; 
                        string[] files = Directory.GetFiles(soundFolder, searchPattern);
                        
                        if (files != null && files.Length > 0)
                        {
                            candidates.AddRange(files);
                        }
                    }

                    // 将结果写入缓存（即使是空列表也缓存，避免反复扫描不存在的文件）
                    _soundCache[cacheKey] = candidates;
                    
                    if (candidates.Count > 0)
                    {
                        CMDebug.Log($"[MaidSounds] 缓存已建立: Key='{cacheKey}', 找到 {candidates.Count} 个文件。");
                    }
                }

                // 2. 如果没有找到对应文件，直接返回 false
                if (candidates == null || candidates.Count == 0) return false;

                // 3. 从缓存列表中随机选取一个
                int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
                string selectedFile = candidates[randomIndex];

                // 4. 执行播放
                // CMDebug.Log($"[MaidSounds] 播放: {Path.GetFileName(selectedFile)}"); // 调试时可开启
                PlayCustomSound3D(selectedFile, source.transform.position);
                return true;
            }
            catch (System.Exception ex)
            {
                CMDebug.Log($"[MaidSounds] 播放异常 ({soundKey}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清理缓存 (如果需要在游戏运行时热重载音频文件，可以调用此方法)
        /// </summary>
        public static void ClearCache()
        {
            _soundCache.Clear();
            CMDebug.Log("[MaidSounds] 语音缓存已清空。");
        }

        // FMOD 播放逻辑保持不变
        private static void PlayCustomSound3D(string filePath, Vector3 position)
        {
            var coreSystem = FMODUnity.RuntimeManager.CoreSystem;
            MODE mode = MODE._3D | MODE._3D_LINEARROLLOFF | MODE.LOOP_OFF | MODE.CREATESTREAM;

            RESULT res = coreSystem.createSound(filePath, mode, out Sound sound);
            if (res != RESULT.OK) return;

            ChannelGroup group = default;
            if (FMODUnity.RuntimeManager.GetBus("bus:/Master/SFX").getChannelGroup(out group) != RESULT.OK)
            {
                coreSystem.getMasterChannelGroup(out group);
            }

            sound.set3DMinMaxDistance(1.5f, 25f);
            res = coreSystem.playSound(sound, group, true, out Channel channel);
            
            if (res == RESULT.OK)
            {
                var fPos = new FMOD.VECTOR { x = position.x, y = position.y, z = position.z };
                var fVel = new FMOD.VECTOR { x = 0, y = 0, z = 0 };
                channel.set3DAttributes(ref fPos, ref fVel);
                channel.setMode(mode);
                channel.setPaused(false);
            }
            else
            {
                sound.release();
            }
        }
    }
}