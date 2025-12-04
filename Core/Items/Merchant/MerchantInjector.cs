using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CombatMaid.Core.Items.Merchant
{
    public class MerchantInjector : MonoBehaviour
    {
        private const string TargetMerchantID = "5052"; // 武器商人ID
        private bool _injected = false;

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _injected = false;
            StartCoroutine(InjectRoutine());
        }

        private IEnumerator InjectRoutine()
        {
            // 等待游戏数据库加载
            yield return new WaitForSeconds(3.0f);
            
            InjectItems();
        }

        private void InjectItems()
        {
            if (_injected) return;

            // 1. 获取商店数据库单例 (利用反射)
            // 这里为了演示清晰，直接写逻辑，建议封装到 Helper
            var dbType = System.Type.GetType("Duckov.Economy.StockShopDatabase, Assembly-CSharp");
            if (dbType == null) return;
            
            var instanceProp = dbType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var database = instanceProp?.GetValue(null);
            if (database == null) return;

            // 2. 获取 "GetMerchantProfile" 方法
            var getProfileMethod = dbType.GetMethod("GetMerchantProfile", 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, 
                null, new[] { typeof(string) }, null);

            if (getProfileMethod == null) return;

            // 3. 获取武器商人的 Profile 对象
            var merchantProfile = getProfileMethod.Invoke(database, new object[] { "Mud" }); // "Mud" 是武器商人的内部名
            if (merchantProfile == null) return;

            // 4. 遍历我们的注册表，把所有物品塞进去
            foreach (var customItem in ItemRegistry.GetAllItems())
            {
                AddItemToProfile(merchantProfile, customItem);
            }

            // 5. 刷新商店 UI (如果有 StockShop 组件在场景里)
            RefreshActiveShops();
            
            _injected = true;
            CMDebug.Log("[MerchantInjector] 商品注入完成");
        }

        private void AddItemToProfile(object profile, CustomItemBase item)
        {
            // 反射获取 ItemEntry 列表
            var entriesField = profile.GetType().GetField("entries", BindingFlags.Public | BindingFlags.Instance);
            var entriesList = entriesField?.GetValue(profile) as System.Collections.IList;
            
            if (entriesList == null) return;

            // 检查是否已经存在
            foreach (var entry in entriesList)
            {
                // 假设 Entry 有 typeID 字段
                var idField = entry.GetType().GetField("typeID");
                if (idField != null && (int)idField.GetValue(entry) == item.ItemID)
                    return; // 这里的id已经有了，跳过
            }

            // 创建新的 Entry (使用 CreateInstance 因为 ItemEntry 是内部类)
            var entryType = entriesList.GetType().GetGenericArguments()[0]; // 获取 List<ItemEntry> 中的 ItemEntry 类型
            var newEntry = System.Activator.CreateInstance(entryType);

            // 设置属性
            SetField(newEntry, "typeID", item.ItemID);
            SetField(newEntry, "priceFactor", 1.0f); // 价格倍率
            SetField(newEntry, "maxStock", item.MaxStock);
            SetField(newEntry, "forceUnlock", true); // 强制解锁
            SetField(newEntry, "possibility", 1.0f); // 100% 出现

            // 加入列表
            entriesList.Add(newEntry);
        }

        private void RefreshActiveShops()
        {
            // 查找场景里所有的商店组件并刷新
            // 这是一个简单的查找逻辑，不需要太复杂的反射
            var shops = FindObjectsOfType<MonoBehaviour>();
            foreach (var shop in shops)
            {
                if (shop.GetType().Name == "StockShop")
                {
                    // 调用 RefreshIfNeeded 或类似方法
                    // 这里简化处理，直接让它重新初始化可能比较暴力，通常注入数据后下次打开UI就会刷新
                    var initMethod = shop.GetType().GetMethod("InitializeEntries", BindingFlags.NonPublic | BindingFlags.Instance);
                    initMethod?.Invoke(shop, null);
                }
            }
        }

        private void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}