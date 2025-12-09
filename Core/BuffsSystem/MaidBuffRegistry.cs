using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.BuffsSystem
{
    public class MaidBuffRegistry
    {
        private static MaidBuffRegistry _instance;
        public static MaidBuffRegistry Instance => _instance ??= new MaidBuffRegistry();

        private Dictionary<string, IMaidBuffEffect> _effects = new Dictionary<string, IMaidBuffEffect>();

        /// <summary>
        /// 在 Mod 启动时调用，注册所有自定义 Buff 效果
        /// </summary>
        public void Initialize()
        {
            RegisterEffect(new CombatMaid.Core.BuffsSystem.Effects.MaidTearEffect());
            RegisterEffect(new CombatMaid.Core.BuffsSystem.Effects.MaidSuperRegenEffect());
            
            CMDebug.Log($"初始化完成，已注册 {_effects.Count} 个自定义Buff效果");
        }
        
        public void Cleanup()
        {
            _effects.Clear();
            CMDebug.Log("Buff注册表已清理");
        }

        public void RegisterEffect(IMaidBuffEffect effect)
        {
            if (effect == null || string.IsNullOrEmpty(effect.BuffName)) return;
            
            if (!_effects.ContainsKey(effect.BuffName))
            {
                _effects.Add(effect.BuffName, effect);
            }
        }

        public IMaidBuffEffect GetEffect(string buffName)
        {
            _effects.TryGetValue(buffName, out var effect);
            return effect;
        }

        public bool IsRegistered(string buffName) => _effects.ContainsKey(buffName);
    }
}