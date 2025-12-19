using System;
using System.Collections.Generic;
using CombatMaid.Core.MaidConfigs;

namespace CombatMaid.Core
{
    [Serializable]
    public class MaidProfileData
    {
        public string ProfileName;
        public MaidConfig PresetConfig;
        public MaidExtraInfo ExtraData;
        public MaidInventoryData Inventory = new MaidInventoryData();
    }
    
    [Serializable]
    public class MaidInventoryData
    {
        // 装备栏
        public List<MaidItemData> Equipment = new List<MaidItemData>();
        // 背包内容
        public List<MaidItemData> InventoryContent = new List<MaidItemData>();
    }

    [Serializable]
    public class MaidItemData
    {
        public int TypeID;           // 物品的数字ID
        public int SlotIndex;        // 在格子里的位置 / 装备槽的索引
        public string SlotKey;       // 装备槽的名称
        
        public int Count;            // 堆叠数量
        public float Durability;     // 耐久度
        public float DurabilityLoss;
        public float MaxDurability;
        public string FromInfoKey;
        public bool Inspected;       // 是否已鉴定
        // 自定义变量列表
        public List<MaidCustomVarData> CustomVariables;
        // 递归数据
        public List<MaidItemData> Attachments;     // 枪械配件
        public List<MaidItemData> InnerContainer;  // 容器内容
    }
    
    [Serializable]
    public class MaidCustomVarData
    {
        public string Key;
        public int TypeEnumVal; // 存储 CustomDataType 的枚举整数值
        public byte[] RawBytes; // 存储原始字节数据
    }
    

    [Serializable]
    public class MaidExtraInfo
    {
        public string Description;

        public string BasePresetKey = "Cname_Usec";

        public string CustomModelID = "";

        public string TacticalMode = "Standard";

        public List<MaidSkillConfig> Skills = new List<MaidSkillConfig>();
        
        public List<string> AppliedModifierKeys = new List<string>();
    }

    [Serializable]
    public class MaidSkillConfig
    {
        public string SkillID;
        public Dictionary<string, object> Params = new Dictionary<string, object>();
    }
}