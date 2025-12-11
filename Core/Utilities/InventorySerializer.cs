using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using Cysharp.Threading.Tasks;
using ItemStatsSystem.Items;

namespace CombatMaid.Core.Utilities
{
    public static class InventorySerializer
    {
        // ... (Serialize 部分保持不变，无需修改) ...
        #region 保存逻辑 (Serialize)
        // ... (此处省略上面的 SerializeCharacter 代码，与上一版相同) ...
        
        public static MaidInventoryData SerializeCharacter(Item characterItem)
        {
            // (代码内容与上一版一致，省略以节省篇幅)
            var data = new MaidInventoryData();
            if (characterItem == null) return data;

            if (characterItem.Slots != null)
            {
                foreach (var slot in characterItem.Slots)
                {
                    if (slot != null && slot.Content != null)
                    {
                        var itemData = ConvertItemToData(slot.Content);
                        itemData.SlotKey = slot.Key; 
                        data.Equipment.Add(itemData);
                    }
                }
            }

            if (characterItem.Inventory != null)
            {
                data.InventoryContent = SerializeInventoryContent(characterItem.Inventory);
            }
            return data;
        }

        private static List<MaidItemData> SerializeInventoryContent(Inventory inventory)
        {
            var list = new List<MaidItemData>();
            if (inventory == null) return list;

            for (int i = 0; i < inventory.Capacity; i++)
            {
                Item item = inventory.GetItemAt(i);
                if (item != null)
                {
                    var itemData = ConvertItemToData(item);
                    itemData.SlotIndex = i;
                    list.Add(itemData);
                }
            }
            return list;
        }

        private static MaidItemData ConvertItemToData(Item item)
        {
            var data = new MaidItemData
            {
                TypeID = item.TypeID,
                Count = item.StackCount,
                Durability = item.Durability,
                Inspected = item.Inspected
            };

            if (item.Slots != null)
            {
                data.Attachments = new List<MaidItemData>();
                foreach (var slot in item.Slots)
                {
                    if (slot != null && slot.Content != null)
                    {
                        var subItemData = ConvertItemToData(slot.Content);
                        subItemData.SlotKey = slot.Key;
                        data.Attachments.Add(subItemData);
                    }
                }
            }

            if (item.Inventory != null)
            {
                data.InnerContainer = SerializeInventoryContent(item.Inventory);
            }

            return data;
        }
        #endregion

        #region 读取逻辑 (Deserialize) - 已修正 Plug 参数

        public static async UniTask DeserializeCharacterAsync(MaidInventoryData data, Item characterItem)
        {
            if (data == null || characterItem == null) return;

            // 1. 恢复装备 (Equipment)
            if (data.Equipment != null)
            {
                foreach (var equipData in data.Equipment)
                {
                    // 查找对应的 Slot
                    // 注意：SlotCollection 实际上是 IEnumerable<Slot>，通常没有 GetSlot(string) 方法
                    // 我们需要遍历查找 Key 匹配的 Slot
                    Slot targetSlot = null;
                    if (characterItem.Slots != null)
                    {
                        foreach (var s in characterItem.Slots)
                        {
                            if (s.Key == equipData.SlotKey)
                            {
                                targetSlot = s;
                                break;
                            }
                        }
                    }

                    if (targetSlot != null)
                    {
                        // 异步创建物品
                        Item item = await CreateItemFromDataAsync(equipData);
                        if (item != null)
                        {
                            // [修正] 使用 Plug(item, out _) 并移除手动 Unplug
                            // Plug 会自动处理卸载，如果我们要丢弃旧物品，用 out _ 忽略即可
                            bool success = targetSlot.Plug(item, out Item oldItem);
                            
                            if (!success)
                            {
                                CMDebug.LogWarning($"无法将物品 {item.TypeID} 安装到槽位 {targetSlot.Key} (Tag不匹配?)");
                                // 失败时最好销毁新创建的物品，防止内存泄漏
                                UnityEngine.Object.Destroy(item.gameObject);
                            }
                            else
                            {
                                // 如果顶替了旧物品且我们不需要它，销毁它
                                if (oldItem != null) UnityEngine.Object.Destroy(oldItem.gameObject);
                            }
                        }
                    }
                }
            }

            // 2. 恢复背包 (Inventory)
            if (data.InventoryContent != null && characterItem.Inventory != null)
            {
                characterItem.Inventory.DestroyAllContent(); // 先清空背包
                
                foreach (var itemData in data.InventoryContent)
                {
                    Item item = await CreateItemFromDataAsync(itemData);
                    if (item != null)
                    {
                        bool success = characterItem.Inventory.AddAt(item, itemData.SlotIndex);
                        if (!success) 
                        {
                            characterItem.Inventory.AddItem(item); // 兜底：如果指定格子满了，找个空位
                        }
                    }
                }
            }
        }

        private static async UniTask<Item> CreateItemFromDataAsync(MaidItemData data)
        {
            // 异步创建
            Item item = await ItemAssetsCollection.InstantiateAsync(data.TypeID);

            if (item == null)
            {
                CMDebug.LogError($"无法创建物品 TypeID: {data.TypeID}");
                return null;
            }

            // 必须先初始化，否则 Slots 等组件可能未就绪
            item.Initialize(); 

            // 恢复基础属性
            if (item.Stackable) item.StackCount = data.Count; 
            if (item.UseDurability) item.Durability = data.Durability;
            item.Inspected = data.Inspected;

            // 递归：恢复配件 (Attachments) -> 枪械改装
            if (data.Attachments != null && item.Slots != null)
            {
                foreach (var attData in data.Attachments)
                {
                    // 查找子槽位
                    Slot subSlot = null;
                    foreach (var s in item.Slots)
                    {
                        if (s.Key == attData.SlotKey)
                        {
                            subSlot = s;
                            break;
                        }
                    }

                    if (subSlot != null)
                    {
                        Item attachment = await CreateItemFromDataAsync(attData);
                        if (attachment != null)
                        {
                            // [修正] 递归中也使用正确的 Plug
                            subSlot.Plug(attachment, out Item oldAtt);
                            if (oldAtt != null) UnityEngine.Object.Destroy(oldAtt.gameObject);
                        }
                    }
                }
            }

            // 递归：恢复内部容器 (InnerContainer) -> 弹挂/背包里的东西
            if (data.InnerContainer != null && item.Inventory != null)
            {
                foreach (var innerData in data.InnerContainer)
                {
                    Item innerItem = await CreateItemFromDataAsync(innerData);
                    if (innerItem != null)
                    {
                        item.Inventory.AddAt(innerItem, innerData.SlotIndex);
                    }
                }
            }

            return item;
        }
        #endregion
    }
}