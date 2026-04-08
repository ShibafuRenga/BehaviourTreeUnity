using System;

namespace Shibafu.BehaviourTree
{
    /// <summary>
    /// 标记可在 JSON 中使用的节点类型 id 及编辑器显示名。挂在任意类上即可被行为树编辑器扫描到。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class BTNodeTypeAttribute : Attribute
    {
        public string TypeId { get; }
        public string DisplayName { get; }

        /// <summary>
        /// 编辑器里类型选择菜单的分组路径（不含最后一级显示名），用 '/' 分段。
        /// 例：<c>Composite</c>、<c>Leaf</c>；与 <see cref="DisplayName"/> 组合为 <c>Composite/顺序 Sequence</c>。
        /// </summary>
        public string EditorMenuPath { get; set; }

        public BTNodeTypeAttribute(string typeId, string displayName = null)
        {
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            DisplayName = string.IsNullOrEmpty(displayName) ? typeId : displayName;
        }
    }
}
