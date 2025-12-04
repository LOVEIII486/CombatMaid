using System.Collections.Generic;
using CombatMaid.Core.Items.Implementations;
using CombatMaid; // [修正1] 引用 CMDebug 所在的命名空间

namespace CombatMaid.Core.Items
{
    public static class ItemRegistry
    {
        private static Dictionary<int, CustomItemBase> _items = new Dictionary<int, CustomItemBase>();
        public static bool IsInitialized { get; private set; }

        public static void Initialize()
        {
            if (IsInitialized) return;

            // [核心注册区]
            
            // 1. 普通女仆契约 (ID: 88888, 预设: Cname_Usec)
            Register(new ContractItem(88888, "Cname_Usec")); 

            // 2. [示例] 狙击手契约 (ID: 88889, 预设: Cname_Sniper)
            // Register(new ContractItem(88889, "Cname_Sniper")); 

            IsInitialized = true;
            CMDebug.Log($"[ItemRegistry] 已注册 {_items.Count} 个自定义物品");
        }

        private static void Register(CustomItemBase item)
        {
            if (!_items.ContainsKey(item.ItemID))
            {
                _items.Add(item.ItemID, item);
                CMDebug.Log($"注册物品: {item.ItemID} | {item.NameKey}");
            }
            else
            {
                CMDebug.LogWarning($"重复注册物品 ID: {item.ItemID}");
            }
        }

        public static CustomItemBase GetDefinition(int id)
        {
            return _items.TryGetValue(id, out var item) ? item : null;
        }
        
        public static IEnumerable<CustomItemBase> GetAllItems() => _items.Values;

        public static bool IsCustomItem(int id) => _items.ContainsKey(id);
    }
}