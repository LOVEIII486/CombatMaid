using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace CombatMaid.Core.Items.Patches
{
    [HarmonyPatch(typeof(Item), "Use")]
    public static class ItemUsagePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Item __instance, object user) // 注意：Use的参数名在源码里是 object user
        {
            var character = user as CharacterMainControl;
            if (__instance == null || character == null) return true;

            // 检查注册表
            var definition = ItemRegistry.GetDefinition(__instance.TypeID);
            if (definition != null)
            {
                // 执行自定义逻辑
                bool success = definition.OnUsed(character);

                // 如果成功且设定为消耗品
                if (success)
                {
                    // 1. 堆叠处理：如果有多个，只减数量
                    if (__instance.StackCount > 1)
                    {
                        __instance.StackCount--;
                    }
                    else
                    {
                        // 2. 数量为1时，先从父容器（背包或插槽）断开连接
                        // Detach() 会自动处理 InInventory.RemoveItem 或 Slot.Unplug
                        __instance.Detach(); 
        
                        // 3. 然后销毁游戏物体
                        UnityEngine.Object.Destroy(__instance.gameObject);
                    }
                }
                
                // 阻止原版 Use 逻辑
                return false; 
            }

            return true;
        }
    }
}