using UnityEngine;
using Duckov.Utilities;
using System.Reflection; // 必须引入反射命名空间
using System.Text;
using ItemStatsSystem;   // 引用 Tag 所在的命名空间

namespace CombatMaid.DebugTools
{
    public static class TagPrinter
    {
        public static void PrintAllTags()
        {
            // 检查 Tags 是否存在
            if (GameplayDataSettings.Tags == null)
            {
                CMDebug.LogError("GameplayDataSettings.Tags 为空，无法获取标签列表。");
                return;
            }

            var allTags = GameplayDataSettings.Tags.AllTags;
            
            CMDebug.LogInfo($"========== 开始深度输出可用标签 (共 {allTags.Count} 个) ==========");

            foreach (Tag tag in allTags)
            {
                if (tag != null)
                {
                    PrintTagDetails(tag);
                }
            }
            
            CMDebug.LogInfo("========== 标签输出结束 ==========");
        }

        /// <summary>
        /// 使用反射打印 Tag 对象的所有公开字段和属性
        /// </summary>
        private static void PrintTagDetails(Tag tag)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- Tag: [{tag.name}] ---");

            // 1. 获取类型信息
            System.Type type = tag.GetType();

            // 2. 获取所有公开属性 (Properties)
            PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (props.Length > 0)
            {
                sb.AppendLine("  [属性 Properties]:");
                foreach (var prop in props)
                {
                    try
                    {
                        // 过滤掉已知的 name，避免重复刷屏，也可以保留
                        object val = prop.GetValue(tag, null);
                        sb.AppendLine($"    - {prop.Name}: {val}");
                    }
                    catch
                    {
                        sb.AppendLine($"    - {prop.Name}: <无法读取>");
                    }
                }
            }

            // 3. 获取所有公开字段 (Fields)
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (fields.Length > 0)
            {
                sb.AppendLine("  [字段 Fields]:");
                foreach (var field in fields)
                {
                    object val = field.GetValue(tag);
                    sb.AppendLine($"    - {field.Name}: {val}");
                }
            }

            // 输出构建好的字符串
            CMDebug.LogInfo(sb.ToString());
        }
    }
}