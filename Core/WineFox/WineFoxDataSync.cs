using UnityEngine;
using CombatMaid.Core;
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

            // 示例：实时同步经验值和血量（如果需要保存当前血量）
            // 注意：这里修改的是内存中的 CurrentData引用
            // _refData.PresetConfig.Exp = _controller.MaidCharacter.Experience; 

            // 定时自动保存 (比如每30秒)
            _saveTimer += Time.deltaTime;
            if (_saveTimer > 30f)
            {
                SyncAndSave();
                _saveTimer = 0f;
            }
        }

        private void OnDestroy()
        {
            // 销毁/死亡/场景切换时保存
            SyncAndSave();
        }

        private void SyncAndSave()
        {
            if (_controller == null || _refData == null) return;

            // 1. 将当前实体的运行时数据反写回 Data 对象
            // 这里你需要根据你的升级逻辑来写，比如：
            // _refData.PresetConfig.Health = _controller.MaidCharacter.Health.MaxHealth; // 如果成长了
            // _refData.PresetConfig.Exp = (int)_controller.MaidCharacter.Experience;
            
            // 2. 执行保存
            WineFoxDataManager.SaveData();
        }
    }
}