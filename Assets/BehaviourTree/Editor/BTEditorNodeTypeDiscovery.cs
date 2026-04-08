using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>
    /// 通过 <see cref="BTNodeTypeAttribute"/> 收集所有程序集中的节点类型（供下拉框使用）。
    /// </summary>
    public static class BTEditorNodeTypeDiscovery
    {
        private const string CustomChoiceLabel = "自定义类型…";

        public static string CustomTypeMenuLabel => CustomChoiceLabel;

        /// <summary>按显示名排序的 (TypeId, DisplayName)。不含「自定义」项。</summary>
        public static IReadOnlyList<(string TypeId, string DisplayName)> GetOrderedEntries()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var type in TypeCache.GetTypesWithAttribute<BTNodeTypeAttribute>())
            {
                var attr = type.GetCustomAttribute<BTNodeTypeAttribute>(false);
                if (attr == null)
                    continue;
                var id = attr.TypeId?.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;
                map[id] = string.IsNullOrEmpty(attr.DisplayName) ? id : attr.DisplayName;
            }

            if (map.Count == 0)
            {
                map["sequence"] = "顺序 Sequence";
                map["selector"] = "选择 Selector";
                map["inverter"] = "取反 Inverter";
                map["action"] = "动作 Action";
                map["condition"] = "条件 Condition";
                map["wait"] = "等待 Wait";
            }

            return map
                .Select(kv => (kv.Key, kv.Value))
                .OrderBy(t => t.Value, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static bool IsKnownTypeId(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
                return false;
            return GetOrderedEntries().Any(e =>
                string.Equals(e.TypeId, typeId, StringComparison.Ordinal));
        }

        /// <summary>仅可作为「父」一侧连线的类型（有子端口），用于从「父」口拖线创建上游节点。</summary>
        public static IReadOnlyList<(string TypeId, string DisplayName)> GetOrderedEntriesWithChildPortOnly()
        {
            return GetOrderedEntries()
                .Where(e => BTGraphEditorNodeLayout.ShowsChildPort(e.TypeId))
                .ToList();
        }

        /// <summary>构建下拉标签列表（末尾为自定义项）。</summary>
        public static void BuildChoiceLists(out List<string> labels, out List<string> typeIds)
        {
            labels = new List<string>();
            typeIds = new List<string>();
            foreach (var e in GetOrderedEntries())
            {
                labels.Add(e.DisplayName);
                typeIds.Add(e.TypeId);
            }

            labels.Add(CustomChoiceLabel);
            typeIds.Add(null);
        }
    }
}
