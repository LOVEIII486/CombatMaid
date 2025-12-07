using System.Collections.Generic;
using CombatMaid.Core.MaidConfigs;

namespace CombatMaid.Core
{
    [System.Serializable]
    public class MaidProfileData
    {
        public string ProfileName;
        public MaidConfig PresetConfig;
        public MaidExtraInfo ExtraData;
    }

    [System.Serializable]
    public class MaidExtraInfo
    {
        public string Description;

        public string BasePresetKey = "Cname_Usec";

        public string CustomModelID = "";

        public string TacticalMode = "Standard";

        public List<MaidSkillConfig> Skills = new List<MaidSkillConfig>();
        
        public List<string> AppliedModifierKeys = new List<string>();
    }

    [System.Serializable]
    public class MaidSkillConfig
    {
        public string SkillID;
        public Dictionary<string, object> Params = new Dictionary<string, object>();
    }
}