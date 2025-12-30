using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Buffs;
using Duckov.Utilities;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// 女仆 Buff 工厂 - 负责实例化和配置 Buff 对象
    /// </summary>
    public static class MaidBuffFactory
    {
        private static readonly Dictionary<string, Buff> SharedBuffs = new Dictionary<string, Buff>();

        private static FieldInfo _idField;
        private static FieldInfo _displayNameField; // 用于设置本地化 Key
        private static FieldInfo _descriptionField; // 用于设置描述 Key
        private static FieldInfo _limitedLifeTimeField;
        private static FieldInfo _totalLifeTimeField;
        private static bool _fieldsInitialized = false;

        public struct BuffConfig
        {
            public string Name;          // 必须以 "MaidBuff_" 开头
            public int Id;               // 唯一的数字 ID
            public float Duration;       // 持续时间
            public bool LimitedLifeTime; // 是否有限时

            public BuffConfig(string name, int id, float duration = 5f)
            {
                Name = name;
                Id = id;
                Duration = duration;
                LimitedLifeTime = duration > 0;
            }
        }

        public static Buff GetOrCreateSharedBuff(BuffConfig config)
        {
            // 生成复合 Key：ID + 是否限时
            // 这样 888004_True (限时) 和 888004_False (永久) 将拥有各自的独立模板
            string cacheKey = $"{config.Id}_{config.LimitedLifeTime}";

            if (SharedBuffs.TryGetValue(cacheKey, out Buff existing))
            {
                if (existing != null) return existing;
                SharedBuffs.Remove(cacheKey);
            }
            return CreateSharedBuff(config, cacheKey);
        }

        private static Buff CreateSharedBuff(BuffConfig config, string cacheKey)
        {
            try
            {
                Buff baseBuff = GameplayDataSettings.Buffs.BaseBuff;
                if (baseBuff == null) return null;

                Buff newBuff = UnityEngine.Object.Instantiate(baseBuff);
                
                newBuff.name = config.LimitedLifeTime ? config.Name : $"{config.Name}_Permanent";
                
                UnityEngine.Object.DontDestroyOnLoad(newBuff.gameObject);

                InitializeReflection();
        
                _idField?.SetValue(newBuff, config.Id);
                _displayNameField?.SetValue(newBuff, $"Buff_{config.Name}_Name");
                _descriptionField?.SetValue(newBuff, $"Buff_{config.Name}_Desc");
                _limitedLifeTimeField?.SetValue(newBuff, config.LimitedLifeTime);
                _totalLifeTimeField?.SetValue(newBuff, config.Duration);

                SharedBuffs[cacheKey] = newBuff;
                return newBuff;
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"创建 Buff {config.Name} 失败: {ex.Message}");
                return null;
            }
        }

        public static bool TryAddBuff(CharacterMainControl target, Buff buffTemplate, CharacterMainControl attacker = null)
        {
            if (target == null || buffTemplate == null) return false;
            try
            {
                target.AddBuff(buffTemplate, attacker, 1);
                return true;
            }
            catch { return false; }
        }

        private static void InitializeReflection()
        {
            if (_fieldsInitialized) return;
            var t = typeof(Buff);
            _idField = t.GetField("id", BindingFlags.Instance | BindingFlags.NonPublic);
            _displayNameField = t.GetField("displayName", BindingFlags.Instance | BindingFlags.NonPublic);
            _descriptionField = t.GetField("description", BindingFlags.Instance | BindingFlags.NonPublic);
            _limitedLifeTimeField = t.GetField("limitedLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
            _totalLifeTimeField = t.GetField("totalLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
            _fieldsInitialized = true;
        }
    }
}