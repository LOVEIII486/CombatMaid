using System.Text;
using ItemStatsSystem;
using Duckov.UI.DialogueBubbles;
using Cysharp.Threading.Tasks;

namespace CombatMaid.Core.Items.Components
{
    /// <summary>
    /// 女仆体检仪
    /// </summary>
    public class Component_MaidStatusChecker : UsageBehavior
    {
        public override bool CanBeUsed(Item item, object user)
        {
            return user is CharacterMainControl && 
                   MaidManager.Instance != null && 
                   MaidManager.Instance.ActiveMaidCount > 0;
        }

        protected override void OnUse(Item item, object user)
        {
            var player = user as CharacterMainControl;
            if (player == null || MaidManager.Instance == null) return;

            var maids = MaidManager.Instance.GetAllActiveMaids();
            if (maids.Count == 0)
            {
                player.PopText("<color=#FF4500>未发现活跃女仆</color>");
                return;
            }

            player.PopText("<color=#FFD700>▌体检中...</color>");

            foreach (var maid in maids)
            {
                ExecuteScanningFlow(maid).Forget();
            }
        }

        private async UniTaskVoid ExecuteScanningFlow(MaidController fox)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.5f));

            if (fox == null || fox.MaidCharacter == null) return;

            var m = fox.MaidCharacter;
            var h = m.Health;
            var ai = fox.AI;
            var sb = new StringBuilder();

            const string Gold = "#FFD700";
            const string Cyan = "#00FFFF";
            const string Green = "#32CD32";
            const string Red = "#FF4500";
            const string White = "#FFFFFF";

            sb.AppendLine($"<color={Gold}><b>[ {m.characterPreset.DisplayName} 状态报告 ]</b></color>");
            sb.AppendLine($"<color={Green}>生命</color> {h.CurrentHealth:F0}/{h.MaxHealth:F0}");
            sb.AppendLine($"<color={Red}>伤害</color> {m.GunDamageMultiplier:F2} <color={Red}>暴击</color> {m.GunCritRateGain:P0}");
            sb.AppendLine($"<color={Gold}>抗性</color> 物{h.ElementFactor(ElementTypes.physics):F1} 火{h.ElementFactor(ElementTypes.fire):F1} 毒{h.ElementFactor(ElementTypes.poison):F1} 电{h.ElementFactor(ElementTypes.electricity):F1} 空{h.ElementFactor(ElementTypes.space):F1} 灵{h.ElementFactor(ElementTypes.ghost):F1}");
            sb.AppendLine($"<color={Cyan}>护甲</color> 头盔[{h.HeadArmor:F1}]  身体[{h.BodyArmor:F1}]");
            sb.AppendLine($"<color={Cyan}>视野</color> 距({ai.sightDistance:F0}m) 角({ai.sightAngle:F0}°) 感({m.SenseRange:F1}m)");
            
            string finalReport = sb.ToString();

            CMDebug.Log(finalReport);
            DialogueBubblesManager.Show(finalReport, fox.transform, 2.8f, false, false, -1f, 10.0f).Forget();
        }
    }
}