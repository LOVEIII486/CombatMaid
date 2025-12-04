using System.Collections.Generic;
using UnityEngine;

namespace CombatMaid.Core.MaidConfigs
{
    /// <summary>
    /// 纯净的预设配置类
    /// </summary>
    [System.Serializable]
    public class MaidConfig
    {
        [Header("--- 核心标识 ---")] 
        public string CustomName = "战斗女仆";
        public bool IsBossIcon = false; 
        public bool ShowName = true;
        public bool ShowHealthBar = true;
        
        [Header("--- 基础属性 ---")]
        public float Health = 500f;
        public float MoveSpeedFactor = 1.1f;
        public bool HasSoul = true;
        public int Exp = 100;
        public bool PushCharacter = false;
        
        [Header("--- 物品与掉落 ---")]
        public List<int> CustomItemIDs = new List<int>(); 
        public int WantItem = -1;
        public bool DropBoxOnDead = true;
        public float HasCashChance = 0f;
        public Vector2Int CashRange = new Vector2Int(0, 0);

        [Header("--- 感知能力 ---")]
        public float SightDistance = 30f;
        public float SightAngle = 120f;
        public float HearingAbility = 1.0f;
        public float NightVisionAbility = 0.5f;
        public float ForgetTime = 8f;

        [Header("--- 反应与射击 ---")]
        public float ReactionTime = 0.15f;
        public float NightReactionTimeFactor = 1.5f;
        public float ShootDelay = 0.1f;
        public Vector2 ShootTimeRange = new Vector2(0.5f, 2.0f);
        public Vector2 ShootTimeSpaceRange = new Vector2(1.0f, 2.0f);
        public bool ShootCanMove = true;
        public bool DefaultWeaponOut = true;
        
        [Header("--- 移动与战术 ---")]
        public float PatrolRange = 10f;
        public float CombatMoveRange = 15f;
        public Vector2 CombatMoveTimeRange = new Vector2(1f, 3f);
        public float PatrolTurnSpeed = 200f;
        public float CombatTurnSpeed = 1200f;
        public bool CanDash = true;
        public Vector2 DashCoolTimeRange = new Vector2(2f, 4f);
        public bool CanTalk = true;

        [Header("--- 战斗数值 ---")]
        public float DamageMultiplier = 1.0f;
        public float BulletSpeedMultiplier = 1.0f;
        public float GunDistanceMultiplier = 1.0f;
        public float GunScatterMultiplier = 0.8f;
        public float ScatterMultiIfTargetRunning = 2f;
        public float ScatterMultiIfOffScreen = 2f;
        public float GunCritRateGain = 0f;
        public float AiCombatFactor = 1f;

        [Header("--- 索敌倾向 ---")]
        public bool SetActiveByPlayerDistance = true;
        public float ForceTracePlayerDistance = 0f;
        [Range(0, 1)] public float MinTraceTargetChance = 1f;
        [Range(0, 1)] public float MaxTraceTargetChance = 1f;

        [Header("--- 技能参数 ---")]
        public bool HasSkill = false;
        public float HasSkillChance = 0f;
        public float SkillSuccessChance = 1f;
        public Vector2 SkillCoolTimeRange = Vector2.one;

        [Header("--- 抗性 (ElementFactor) ---")]
        public float ResistPhysics = 1f;
        public float ResistFire = 1f;
        public float ResistPoison = 1f;
        public float ResistElectricity = 1f;
        public float ResistSpace = 1f;
        public float ResistGhost = 1f;
    }
}