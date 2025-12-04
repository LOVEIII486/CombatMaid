using HarmonyLib;
using ItemStatsSystem;

namespace CombatMaid.Core.Items.Patches
{
    /// <summary>
    /// 拦截 ItemAssetsCollection.InstantiateSync
    /// 如果请求的是我们的ID，直接用 ItemBuilder 生成并返回，跳过原版查找
    /// </summary>
    [HarmonyPatch(typeof(ItemAssetsCollection), "InstantiateSync")]
    public static class ItemFactoryPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(int typeID, ref Item __result)
        {
            // 检查是否是我们的物品
            var definition = ItemRegistry.GetDefinition(typeID);
            
            if (definition != null)
            {
                try
                {
                    // 使用 ItemBuilder 构建
                    __result = definition.Build();
                    // 必须激活 GameObject，否则可能不显示
                    if (__result != null && !__result.gameObject.activeSelf)
                    {
                        __result.gameObject.SetActive(true);
                    }
                    return false; // 拦截成功，不再执行原版逻辑
                }
                catch (System.Exception ex)
                {
                    CMDebug.LogError($"构建自定义物品 {typeID} 失败: {ex}");
                }
            }
            return true; // 继续执行原版逻辑
        }
    }
}