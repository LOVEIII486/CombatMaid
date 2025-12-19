using UnityEngine;
using CombatMaid.Core.Utilities;

namespace CombatMaid.Core.WineFox
{
    public class WineFoxDataSync : MonoBehaviour
    {
        private MaidController _controller;
        private MaidProfileData _refData;
        private float _saveTimer = 0f;

        public void Initialize(MaidController controller, MaidProfileData data)
        {
            _controller = controller;
            _refData = data;
        }

        private void Update()
        {
            if (_controller == null || _controller.MaidCharacter == null) return;

            // 实时同步逻辑
            if (_refData != null && _refData.PresetConfig != null)
            {
                // 暂无
            }

            // 定时保存
            _saveTimer += Time.deltaTime;
            if (_saveTimer > 60f)
            {
                SyncAndSave();
                _saveTimer = 0f;
            }
        }

        private void OnDestroy()
        {
            SyncAndSave();
        }

        private void SyncAndSave()
        {
            if (_controller == null || _refData == null) return;
            
            SaveInventory(); 
            WineFoxDataManager.SaveData();
        }
        
        public void SaveInventory()
        {
            if (_controller == null || _controller.MaidCharacter == null) return;
            var character = _controller.MaidCharacter;

            if (character.CharacterItem == null) return;
    
            // CMDebug.LogInfo("正在保存女仆背包");
            
            var data = InventorySerializer.SerializeCharacter(character.CharacterItem);
    
            if (WineFoxDataManager.CurrentData != null)
            {
                WineFoxDataManager.CurrentData.Inventory = data;
                // 注意：这里只更新内存数据，实际写入磁盘由 SyncAndSave 或外部调用 WineFoxDataManager.SaveData() 触发
            }
        }

        public async void LoadInventory() 
        {
            if (WineFoxDataManager.CurrentData?.Inventory == null) return;
            
            if (_controller == null || _controller.MaidCharacter == null) return;
            var character = _controller.MaidCharacter;

            if (character.CharacterItem == null) return;

            CMDebug.LogInfo("正在恢复女仆背包...");
            
            // 执行异步恢复
            await InventorySerializer.DeserializeCharacterAsync(
                WineFoxDataManager.CurrentData.Inventory, 
                character.CharacterItem
            );
            
            CMDebug.LogInfo("女仆背包恢复完成。");
        }
    }
}