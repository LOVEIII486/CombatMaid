using System.Collections.Generic;
using UnityEngine;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using Cysharp.Threading.Tasks;

namespace CombatMaid.Core.Utilities
{
    public static class InventorySerializer
    {
        #region 保存逻辑 (Serialize)

        /// <summary>
        /// 将角色的物品数据（装备栏 + 背包）序列化为数据对象
        /// </summary>
        public static MaidInventoryData SerializeCharacter(Item characterItem)
        {
            // 初始化对象，确保即便没有任何物品，这也是一个空对象而不是 null
            var data = new MaidInventoryData
            {
                Equipment = new List<MaidItemData>(),
                InventoryContent = new List<MaidItemData>()
            };

            if (characterItem == null) return data;

            // 1. 保存装备栏
            if (characterItem.Slots != null)
            {
                foreach (var slot in characterItem.Slots)
                {
                    // 只保存有东西的槽位
                    if (slot != null && slot.Content != null)
                    {
                        var itemData = ConvertItemToData(slot.Content);
                        itemData.SlotKey = slot.Key; // 记录槽位名称 (关键 ID)
                        data.Equipment.Add(itemData);
                    }
                }
            }

            // 2. 保存背包内容
            if (characterItem.Inventory != null)
            {
                data.InventoryContent = SerializeInventoryContent(characterItem.Inventory);
            }

            return data;
        }

        /// <summary>
        /// 递归保存一个 Inventory 的内容
        /// </summary>
        private static List<MaidItemData> SerializeInventoryContent(Inventory inventory)
        {
            var list = new List<MaidItemData>();
            if (inventory == null) return list;

            // 遍历 Inventory 的所有格子
            for (int i = 0; i < inventory.Capacity; i++)
            {
                Item item = inventory.GetItemAt(i);
                if (item != null)
                {
                    var itemData = ConvertItemToData(item);
                    itemData.SlotIndex = i; // 记录物品在背包网格中的具体位置
                    list.Add(itemData);
                }
            }
            return list;
        }

        /// <summary>
        /// 将单个 Item 转换为数据包 (递归处理配件和容器)
        /// </summary>
        private static MaidItemData ConvertItemToData(Item item)
        {
            var data = new MaidItemData
            {
                TypeID = item.TypeID,
                Count = item.StackCount,
                Durability = item.Durability,
                Inspected = item.Inspected,
                // 预先初始化列表，避免 JSON 中出现 null
                Attachments = new List<MaidItemData>(),
                InnerContainer = new List<MaidItemData>()
            };

            // A. 递归保存配件
            if (item.Slots != null)
            {
                foreach (var slot in item.Slots)
                {
                    if (slot != null && slot.Content != null)
                    {
                        var subItemData = ConvertItemToData(slot.Content);
                        subItemData.SlotKey = slot.Key; // 必须记录插在哪个槽上
                        data.Attachments.Add(subItemData);
                    }
                }
            }

            // B. 递归保存容器内容 - 例如弹挂里装的子弹、背包里装的垃圾
            if (item.Inventory != null)
            {
                data.InnerContainer = SerializeInventoryContent(item.Inventory);
            }

            return data;
        }

        #endregion

        #region 读取逻辑 (Deserialize)

        /// <summary>
        /// 异步恢复角色的物品数据
        /// </summary>
        public static async UniTask DeserializeCharacterAsync(MaidInventoryData data, Item characterItem)
        {
            if (data == null || characterItem == null) return;

            // 1. 恢复装备 (Equipment)
            if (data.Equipment != null)
            {
                foreach (var equipData in data.Equipment)
                {
                    // 查找对应的槽位 (Slot)
                    Slot targetSlot = null;
                    if (characterItem.Slots != null)
                    {
                        // 手动遍历查找 Key 匹配的 Slot
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
                            // 使用 Plug 方法安装装备
                            // out Item oldItem 用于接收被顶替下来的旧装备
                            bool success = targetSlot.Plug(item, out Item oldItem);
                            
                            if (!success)
                            {
                                CMDebug.LogWarning($"无法将物品 {item.TypeID} (Data) 安装到槽位 {targetSlot.Key}。可能是Tag不匹配。");
                                Object.Destroy(item.gameObject);
                            }
                            else
                            {
                                // 如果成功安装且顶替了旧物品，销毁旧物品
                                if (oldItem != null) Object.Destroy(oldItem.gameObject);
                            }
                        }
                    }
                }
            }

            // 2. 恢复背包
            if (data.InventoryContent != null && characterItem.Inventory != null)
            {
                // 恢复前先清空背包，防止物品重复或冲突
                characterItem.Inventory.DestroyAllContent(); 
                foreach (var itemData in data.InventoryContent)
                {
                    Item item = await CreateItemFromDataAsync(itemData);
                    if (item != null)
                    {
                        // 尝试放入原来的格子位置
                        bool success = characterItem.Inventory.AddAt(item, itemData.SlotIndex);
                        
                        // 如果原来的位置被占用了，则找个空位放入
                        if (!success) 
                        {
                            characterItem.Inventory.AddItem(item); 
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 核心：根据数据重建 Item 对象
        /// </summary>
        private static async UniTask<Item> CreateItemFromDataAsync(MaidItemData data)
        {
            // 使用游戏原生的异步工厂创建物品
            Item item = await ItemAssetsCollection.InstantiateAsync(data.TypeID);

            if (item == null)
            {
                CMDebug.LogError($"[InventorySerializer] 无法创建物品，TypeID 无效: {data.TypeID}");
                return null;
            }

            // 必须先初始化，确保 Slots/Inventory 组件就绪
            item.Initialize(); 

            // 恢复基础属性
            if (item.Stackable) item.StackCount = data.Count; 
            if (item.UseDurability) item.Durability = data.Durability;
            item.Inspected = data.Inspected;

            // A. 递归恢复配件 (Attachments) -> 枪械改装
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
                            // 递归安装配件
                            subSlot.Plug(attachment, out Item oldAtt);
                            // 销毁被顶替的默认配件（如果有）
                            if (oldAtt != null) Object.Destroy(oldAtt.gameObject);
                        }
                    }
                }
            }

            // B. 递归恢复内部容器 (InnerContainer) -> 弹挂/背包里的东西
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