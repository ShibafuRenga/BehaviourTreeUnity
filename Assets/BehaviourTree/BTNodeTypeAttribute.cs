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

        public BTNodeTypeAttribute(string typeId, string displayName = null)
        {
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            DisplayName = string.IsNullOrEmpty(displayName) ? typeId : displayName;
        }
    }
}
