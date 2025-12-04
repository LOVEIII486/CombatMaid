using UnityEngine;
using Duckov.ItemBuilders;
using ItemStatsSystem;
using CombatMaid.Core; // 引用 MaidManager 所在的命名空间

namespace CombatMaid.Core.Items.Implementations
{
    public class ContractItem : CustomItemBase
    {
        private int _id;
        private string _maidProfileKey;

        // [修改] 构造函数接收 ID 和 预设名
        public ContractItem(int id, string maidProfileKey = "Cname_Usec")
        {
            _id = id;
            _maidProfileKey = maidProfileKey;
        }

        public override int ItemID => _id;
        public override string NameKey => "Item_MaidContract_Name";
        public override int Price => 5000;
        
        public override Sprite GetIcon()
        {
            var refItem = ItemAssetsCollection.GetPrefab(83); 
            return refItem != null ? refItem.Icon : null;
        }

        protected override void OnBuild(ItemBuilder builder)
        {
            // 暂时不需要这里传参了，因为 MaidManager 会读取它自己的 JSON 配置
            // 如果以后你想让不同的契约生成不同的女仆，可以在 SpawnMaidAt 里加参数支持
            builder.SetConstant("ConsumeOnUse", true, false); 
        }

        public override bool OnUsed(CharacterMainControl user)
        {
            // 1. 计算生成位置 (玩家前方 2 米处)
            // 使用 user.transform.forward 保证生成在玩家面前
            Vector3 spawnPos = user.transform.position + user.transform.forward * 2.0f;
            
            // 2. 调用 ModManager 的核心生成逻辑
            // 这会自动处理：读取JSON配置、挂载MaidController、加载模型
            if (MaidManager.Instance != null)
            {
                MaidManager.Instance.SpawnMaidAt(spawnPos);
                user.PopText("契约已签订！");
                return true;
            }
            else
            {
                CMDebug.LogError("MaidManager 实例未找到！");
                return false;
            }
        }
    }
}