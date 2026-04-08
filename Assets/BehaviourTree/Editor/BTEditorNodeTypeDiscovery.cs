using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shibafu.BehaviourTree.Editor
{
    /// <summary>编辑器「类型」一项：含 GenericMenu / DropdownMenu 用的完整分层路径。</summary>
    public readonly struct BTEditorTypeEntry
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string EditorMenuPath { get; }

        public BTEditorTypeEntry(string typeId, string displayName, string editorMenuPath)
        {
            TypeId = typeId;
            DisplayName = displayName;
            EditorMenuPath = editorMenuPath ?? "";
        }
    }

    /// <summary>
    /// 通过 <see cref="BTNodeTypeAttribute"/> 收集节点类型（供菜单、Inspector 下拉使用）。
    /// 排除未带特性的抽象类（如 <see cref="BTAction"/>）。JSON 的 <c>type: "action"</c>（委托 handler）不作为图节点类型，请用「自定义类型」或手写 JSON。
    /// </summary>
    public static class BTEditorNodeTypeDiscovery
    {
        private const string CustomChoiceLabel = "自定义类型…";

        public static string CustomTypeMenuLabel => CustomChoiceLabel;

        /// <summary>菜单项完整路径：<c>分组/显示名</c>；无分组时仅为显示名。</summary>
        public static string FullMenuPath(BTEditorTypeEntry e)
        {
            var p = e.EditorMenuPath?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(p))
                return e.DisplayName;
            return p + "/" + e.DisplayName;
        }

        /// <summary>具体可实例化、带 <see cref="BTNodeTypeAttribute"/> 的类型（按菜单路径排序）。</summary>
        public static IReadOnlyList<BTEditorTypeEntry> GetEditorTypeEntries()
        {
            var byId = new Dictionary<string, BTEditorTypeEntry>(StringComparer.Ordinal);

            foreach (var type in TypeCache.GetTypesWithAttribute<BTNodeTypeAttribute>())
            {
                if (ShouldSkipForEditorTypePicker(type))
                    continue;

                var attr = type.GetCustomAttribute<BTNodeTypeAttribute>(false);
                if (attr == null)
                    continue;

                var id = attr.TypeId?.Trim();
                if (string.IsNullOrEmpty(id))
                    continue;
                // 委托型 leaf，运行时由 handler 解析，不在图编辑器里当可选节点
                if (string.Equals(id, "action", StringComparison.Ordinal))
                    continue;

                var display = string.IsNullOrEmpty(attr.DisplayName) ? id : attr.DisplayName;
                var path = attr.EditorMenuPath?.Trim() ?? "";
                var entry = new BTEditorTypeEntry(id, display, path);

                if (!byId.ContainsKey(id))
                    byId[id] = entry;
            }

            if (byId.Count == 0)
                AddFallbackEntries(byId);

            return byId.Values
                .OrderBy(e => FullMenuPath(e), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void AddFallbackEntries(Dictionary<string, BTEditorTypeEntry> byId)
        {
            void Add(string id, string display, string path)
            {
                if (!byId.ContainsKey(id))
                    byId[id] = new BTEditorTypeEntry(id, display, path);
            }

            Add("sequence", "顺序 Sequence", "Composite");
            Add("selector", "选择 Selector", "Composite");
            Add("inverter", "取反 Inverter", "Composite");
            Add("condition", "条件 Condition", "Condition");
            Add("wait", "等待 Wait", "Leaf");
        }

        /// <summary>普通 abstract class 不进菜单（避免 BTAction/BTComposite 等基类出现在列表）。</summary>
        private static bool ShouldSkipForEditorTypePicker(Type type)
        {
            if (type == null)
                return true;
            if (!type.IsAbstract)
                return false;
            return !type.IsSealed;
        }

        /// <summary>与菜单顺序一致（按 <see cref="FullMenuPath"/>）的 (TypeId, DisplayName)。不含「自定义」项。</summary>
        public static IReadOnlyList<(string TypeId, string DisplayName)> GetOrderedEntries()
        {
            return GetEditorTypeEntries()
                .Select(e => (e.TypeId, e.DisplayName))
                .ToList();
        }

        /// <summary>仅含可挂子节点的类型；顺序同菜单。</summary>
        public static IReadOnlyList<(string TypeId, string DisplayName)> GetOrderedEntriesWithChildPortOnly()
        {
            return GetEditorTypeEntriesWithChildPortOnly()
                .Select(e => (e.TypeId, e.DisplayName))
                .ToList();
        }

        public static bool IsKnownTypeId(string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
                return false;
            return GetEditorTypeEntries().Any(e =>
                string.Equals(e.TypeId, typeId, StringComparison.Ordinal));
        }

        public static IReadOnlyList<BTEditorTypeEntry> GetEditorTypeEntriesWithChildPortOnly()
        {
            return GetEditorTypeEntries()
                .Where(e => BTGraphEditorNodeLayout.ShowsChildPort(e.TypeId))
                .ToList();
        }

        /// <summary>构建下拉标签列表（末尾为自定义项）；标签为完整菜单路径便于区分分组。</summary>
        public static void BuildChoiceLists(out List<string> labels, out List<string> typeIds)
        {
            labels = new List<string>();
            typeIds = new List<string>();
            foreach (var e in GetEditorTypeEntries())
            {
                labels.Add(FullMenuPath(e));
                typeIds.Add(e.TypeId);
            }

            labels.Add(CustomChoiceLabel);
            typeIds.Add(null);
        }

        public static void AppendCreateNodeActions(
            UnityEngine.UIElements.DropdownMenu menu,
            Action<string> onPickType,
            Action onPickCustom,
            bool includeCustomSlot,
            IReadOnlyList<BTEditorTypeEntry> entries = null)
        {
            entries ??= GetEditorTypeEntries();
            foreach (var e in entries)
            {
                var path = FullMenuPath(e);
                var tid = e.TypeId;
                menu.AppendAction(path, _ => onPickType(tid), UnityEngine.UIElements.DropdownMenuAction.AlwaysEnabled);
            }

            if (includeCustomSlot)
            {
                menu.AppendSeparator("");
                menu.AppendAction(CustomChoiceLabel, _ => onPickCustom(), UnityEngine.UIElements.DropdownMenuAction.AlwaysEnabled);
            }
        }

        public static void PopulateGenericMenuCreateNodes(
            GenericMenu menu,
            Action<string> onPickType,
            Action onPickCustom,
            bool includeCustomSlot,
            IReadOnlyList<BTEditorTypeEntry> entries = null)
        {
            entries ??= GetEditorTypeEntries();
            foreach (var e in entries)
                menu.AddItem(new GUIContent(FullMenuPath(e)), false, () => onPickType(e.TypeId));

            if (includeCustomSlot)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(CustomChoiceLabel), false, () => onPickCustom());
            }
        }
    }
}
