using UnityEngine;
using CombatMaid.Core;
using CombatMaid.Core.Utilities;
using CombatMaid.Core.WineFox;

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

            // 1. 实时同步逻辑
            // [安全原则] 只同步"成长性数据" (如经验)，绝对不要同步"属性数据" (如MaxHealth)
            // 因为属性数据包含技能树加成，一旦保存，下次加载会导致双重叠加！
            if (_refData != null && _refData.PresetConfig != null)
            {
                // 假设你的 CharacterMainControl 有 Experience 字段
                // _refData.PresetConfig.Exp = (int)_controller.MaidCharacter.Experience; 
                
                // 如果你有等级系统，也可以同步等级
                // _refData.PresetConfig.Level = ...
            }

            // 2. 定时保存 (每60秒一次足够了，太频繁影响性能)
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

            // [重要] 这里不需要再赋值了，因为Update里已经同步了内存数据
            // 直接调用管理器保存内存中的 _refData 即可
            WineFoxDataManager.SaveData();
        }
        
        public void SaveInventory()
        {
            // 1. [修正] 通过 _controller 获取 MaidCharacter
            if (_controller == null || _controller.MaidCharacter == null) return;
            var character = _controller.MaidCharacter;

            // 2. 检查 CharacterItem 是否存在
            if (character.CharacterItem == null) return;
    
            // CMDebug.LogInfo("正在保存女仆背包..."); // 频繁调用时建议注释掉日志
            
            // 3. 序列化
            var data = InventorySerializer.SerializeCharacter(character.CharacterItem);
    
            // 4. 存入全局数据对象
            if (WineFoxDataManager.CurrentData != null)
            {
                WineFoxDataManager.CurrentData.Inventory = data;
                // 注意：这里只更新内存数据，实际写入磁盘由 SyncAndSave 或外部调用 WineFoxDataManager.SaveData() 触发
            }
        }

        public async void LoadInventory() 
        {
            // 1. 检查存档是否存在
            if (WineFoxDataManager.CurrentData?.Inventory == null) return;
            
            // 2. [修正] 通过 _controller 获取 MaidCharacter
            if (_controller == null || _controller.MaidCharacter == null) return;
            var character = _controller.MaidCharacter;

            if (character.CharacterItem == null) return;

            CMDebug.LogInfo("正在恢复女仆背包...");
            
            // 3. 执行异步恢复
            await InventorySerializer.DeserializeCharacterAsync(
                WineFoxDataManager.CurrentData.Inventory, 
                character.CharacterItem
            );
            
            CMDebug.LogInfo("女仆背包恢复完成。");
        }
    }
}