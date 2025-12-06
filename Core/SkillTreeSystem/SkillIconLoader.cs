using System.Collections.Generic;
using System.IO;
using CombatMaid;
using UnityEngine;

namespace CombatMaid.Core.SkillTreeSystem
{
    public static class SkillIconLoader
    {
        private static Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static Sprite _defaultIcon;

        /// <summary>
        /// 从 assets/textures/skills/ 目录加载图标
        /// </summary>
        public static Sprite LoadIcon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return GetDefaultIcon();
            if (_cache.ContainsKey(fileName)) return _cache[fileName];

            string root = ModBehaviour.Instance != null ? ModBehaviour.Instance.ModRootPath : ".";
            string path = Path.Combine(root, "assets", "textures", "skills", fileName);

            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
                    sprite.name = fileName;
                    _cache[fileName] = sprite;
                    return sprite;
                }
            }
            else
            {
                CMDebug.LogWarning($"[SkillIconLoader] 找不到图标文件: {path}");
            }

            return GetDefaultIcon();
        }

        private static Sprite GetDefaultIcon()
        {
            if (_defaultIcon != null) return _defaultIcon;
            // 创建一个临时的白色方块作为默认图标
            Texture2D tex = new Texture2D(64, 64);
            _defaultIcon = Sprite.Create(tex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
            return _defaultIcon;
        }
    }
}