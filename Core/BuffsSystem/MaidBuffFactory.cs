using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.Buffs;
using Duckov.Utilities;
using UnityEngine;

namespace CombatMaid.Core.BuffsSystem
{
    /// <summary>
    /// 女仆 Buff 工厂 - 负责实例化和配置 Buff 对象
    /// </summary>
    public static class MaidBuffFactory
    {
        // 缓存已创建的 Buff 模板 (单例)
        private static readonly Dictionary<int, Buff> SharedBuffs = new Dictionary<int, Buff>();

        // 反射字段缓存
        private static FieldInfo _idField;
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

        /// <summary>
        /// 获取或创建共享 Buff 模板
        /// </summary>
        public static Buff GetOrCreateSharedBuff(BuffConfig config)
        {
            if (SharedBuffs.TryGetValue(config.Id, out Buff existing))
            {
                if (existing != null) return existing;
                SharedBuffs.Remove(config.Id);
            }
            return CreateSharedBuff(config);
        }

        private static Buff CreateSharedBuff(BuffConfig config)
        {
            try
            {
                // 借用游戏的 BaseBuff 作为模板
                Buff baseBuff = GameplayDataSettings.Buffs.BaseBuff;
                if (baseBuff == null)
                {
                    CMDebug.LogError($"严重错误：BaseBuff 未找到");
                    return null;
                }

                Buff newBuff = UnityEngine.Object.Instantiate(baseBuff);
                newBuff.name = config.Name;
                UnityEngine.Object.DontDestroyOnLoad(newBuff.gameObject);

                InitializeReflection();
        
                _idField?.SetValue(newBuff, config.Id);
                _limitedLifeTimeField?.SetValue(newBuff, config.LimitedLifeTime);
                _totalLifeTimeField?.SetValue(newBuff, config.Duration);

                SharedBuffs[config.Id] = newBuff;
                return newBuff;
            }
            catch (Exception ex)
            {
                CMDebug.LogError($"创建失败: {ex.Message}");
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
            _limitedLifeTimeField = t.GetField("limitedLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
            _totalLifeTimeField = t.GetField("totalLifeTime", BindingFlags.Instance | BindingFlags.NonPublic);
            _fieldsInitialized = true;
        }
    }
}